using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Media;
using System.Windows.Controls;
using Org.BouncyCastle.Asn1.Ocsp;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static int delayValue; //переменная задержки
        private InfoWindow infoWindow; // Поле для хранения ссылки на информационное окно
        public bool isAppAlreadyRunning = false; // Флаг, указывающий, что есть окно "Приложение уже запущено". Это поможет обойти окно "Вы хотите закрыть приложение" в функции OnClosing()
        private bool MenuItemAdded = false; // Флаг для отслеживания состояния кнопки "Остановить" в контекстном меню иконки в трее
        private static List<Connection> _connections = new List<Connection>();
        bool ServerStatus = true; // булевая переменная которую будем использовать чтобы понимать было ли подключение в прошлом цикле удачным. Нужно для отображения окна ошибки/успеха без спама.
        bool isRequestInProgress = false; // булевая переменная которая будет иметь данные о том выполняется ли сейчас попытка подключения к серверу. для контроля асинхронных функций.
        bool FirstStart = true; // Переменная определяющая что приложение только что запустили, нужно для корректной обработки при автозапуске
        bool SettingsChanged = false; //Переменная которая будет менять значение если настройки приложения бы

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // для кнопки "стоп", для отмены потока моментально после нажатия "стоп"(чтобы из-за задержки не багавало)
                                                                                                 // Обработчик события для кнопки "Ок"
        public static MainWindow Instance { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
        }
        protected override void OnStateChanged(System.EventArgs e) // При свёртывании окна приложения значок на панели пуск и в alt-tab будет пропадать.
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowStyle = WindowStyle.ToolWindow;
                ShowInTaskbar = false;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ShowInTaskbar = true;
            }
            base.OnStateChanged(e);
        }
        protected override void OnClosing(CancelEventArgs e) // Обработка при закрытии приложения. Для подтверждения выхода.
        {
            MessageBoxResult result = MessageBox.Show("Вы действительно хотите закрыть приложение?",
                                                      "Подтверждение закрытия", MessageBoxButton.YesNo,
                                                      MessageBoxImage.Information);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                Application.Current.Shutdown();
                base.OnClosing(e);
            }
        }
        public class Connection // Класс Подключений со сведением о подключаемом сервере.
        {
            public string IPAddress { set; get; }
            public string Login { set; get; }
            public string Password { set; get; } 
        }
        public class Mistake  // Класс для записи "Ip адрес-ошибка"
        {
            public string IPAddress { set; get; }
            public string typeofmistake { set; get; }
        }
        private async void StartButton_Click(object sender, EventArgs e) //Кнопка "Старт"
        {
            // Проверяем наличие параметра "-minimized" в командной строке(Такой параметр будет иметь ярлык в папке автозапуска)
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.ToLower() == "-minimized" && FirstStart)
                {
                    WindowState = WindowState.Minimized;
                    WindowStyle = WindowStyle.ToolWindow;
                    ShowInTaskbar = false;
                    FirstStart = false;
                    break;
                }
            }
            MainWindow.Instance.ConsoleTextBox.Text += $"[{DateTime.Now.ToString("HH:mm:ss")}] Выполнение подключения запущено.\n";
            string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SSOInformator", "Settings.ini"); // Путь к файлу Settings
            string error = "";
            error = ReadSettingsFromFile(error, settingsPath); //Вызов функции записи данных из Settings.txt в классы
            if (!string.IsNullOrEmpty(error)) // обработка если была проблема с settings.txt
            {
                StopButton_Click(StopButton, EventArgs.Empty);
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                this.WindowState = WindowState.Normal;
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();                            // Открытия окна с настройками
                AddStartToContexMenu(this, new RoutedEventArgs()); // добавление кнопки "Запустить" в контекстное меню треи
                return;
            }
            StopButton.IsEnabled = true; // Активация кнопки "стоп"
            StartButton.IsEnabled = false; // Блок кнопки "старт"
            List<Mistake> mistakes = new List<Mistake>(); //создание списка для записи айпишников неудачных подключений и видов ошибок для дальнейшей отправки в одно письмо
            cancellationTokenSource = new CancellationTokenSource(); // для отмены потока моментально после нажатия "стоп"
 
            if (MenuItemAdded) // Удаление "Запустить" из контекстного меню иконки в трее
            {
                App app = Application.Current as App;
                var contextMenu = app._notifyIcon.ContextMenu;
                var startMenuItem = contextMenu.MenuItems
                    .OfType<System.Windows.Forms.MenuItem>()
                    .FirstOrDefault(item => item.Text == "Запустить");

                if (startMenuItem != null)
                {
                    contextMenu.MenuItems.Remove(startMenuItem);
                }

                MenuItemAdded = false; // Устанавливаем флаг в false, чтобы пометить, что кнопка "Запустить" была удалена
            }
            AddStopToContexMenu(this, new RoutedEventArgs()); //добавить "Остановить" в контекстное меню
            while (cancellationTokenSource != null && !isRequestInProgress) // Беск.цикл  подключающийся к ip-адресам и отправляющий сообщение ошибки/успеха
            {            // сообщение ошибки показывается в том случае если это первый сбой в подключении к IP. Т.е. подключение к этому IP-адресу в прошлом цикле было успешным.
                         // аналогично с сообщением об успехе
                foreach (Connection conn in _connections)
                {
                    if (cancellationTokenSource != null)
                    {
                        await ConnectToFtpServerAsync(conn, mistakes);
                    }
                }
                mistakes.Clear();   //очищаем весь класс проблемных айпишников для след. цикла
                try
                {
                    if (SettingsChanged) //Если во время попытки подключения были изменены настройки то таймер пропускается, т.к. сама функция подключения не должна писать о состоянии сервера со старыми настройками
                    {
                        SettingsChanged = false;
                    }
                    else if (cancellationTokenSource != null)
                    {
                        if (!SettingsChanged)
                        {
                            await Task.Delay(delayValue, cancellationTokenSource.Token); // Задержка с мониторингом отмены потока
                        }
                        else
                        {
                            SettingsChanged = false;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Если отменили поток(нажали кнопку "стоп")
                    return; // Завершить всю функцию кнопки "старт"
                }
            }
        }
        private async Task ConnectToFtpServerAsync(Connection conn, List<Mistake> mistakes) // функция подключения к серверу
        {
            string ErrorMassege = "";
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + conn.IPAddress); //
            request.Credentials = new NetworkCredential(conn.Login, conn.Password);              // параметры подключения
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;                         //
            bool serverIsStableBeforeConnection = serverIsStable;
            await Task.Run(() =>
            {
                try
                {
                    var response = (FtpWebResponse)request.GetResponse(); //само подключение

                    using (response) // блок если подключение удалось
                    {
                        request.Abort();
                        if (cancellationTokenSource != null || !SettingsChanged) //Если во время попытки подключения была нажата кнопка "Стоп" или изменены настройки то не нужно фиксировать попытку
                        {
                            serverIsStable = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    request.Abort();
                    if (cancellationTokenSource != null || !SettingsChanged) //Если во время попытки подключения была нажата кнопка "Стоп" или изменены настройки то не нужно фиксировать попытку
                    {
                        ErrorMassege = ex.Message;
                        serverIsStable = false;
                    }
                }
            });
            if (cancellationTokenSource == null || SettingsChanged) //Если во время попытки подключения была нажата кнопка "Стоп" или изменены настройки выйти из функции
            {
                request.Abort();
                return;
            }
            if (serverIsStable) //Если подключение удачно вывести в консоль об этом
            {
                ChangeTrayIconOnStable(this, new RoutedEventArgs());
                MainWindow.Instance.ConsoleTextBox.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + "Подключение к IP: " + conn.IPAddress + " выполнено успешно!\n";
            }
            else //Если подключение неудачно вывести в консоль об этом
            {
                MainWindow.Instance.ConsoleTextBox.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + "Ошибка подключения к IP: " + conn.IPAddress + " Ошибка: " + ErrorMassege + "\n";
                ChangeTrayIconOnError(this, new RoutedEventArgs());
            }
            if (serverIsStableBeforeConnection == true && serverIsStable == false) //Если в прошлом подключении сервер был доступен, а в этом нет показать всплывающее окно
            {
                Mistake mist = new Mistake();
                mist.IPAddress = conn.IPAddress;
                mist.typeofmistake = ErrorMassege;
                mistakes.Add(mist);

                ErrorWindow errorWindow = new ErrorWindow(mistakes);
                errorWindow.Topmost = true;
                errorWindow.Show();

                try
                {
                    SoundPlayer errorSound = new SoundPlayer("Resources/error_sound.wav");
                    errorSound.Play();
                }
                catch (Exception)
                {
                    // Звук не проигрался
                }
            }
            else if (serverIsStableBeforeConnection == false && serverIsStable == true) //Если в прошлом подключении сервер был недоступен, а в этом стал доступен показать всплывающее окно
            {
                Mistake mist = new Mistake();
                mist.IPAddress = conn.IPAddress;
                mistakes.Add(mist);
                SuccessWindow successWindow = new SuccessWindow(mistakes);
                successWindow.Topmost = true;
                successWindow.Show();
                try
                {
                    SoundPlayer successSound = new SoundPlayer("Resources/success_sound.wav");
                    successSound.Play();
                }
                catch (Exception)
                {
                    // Звук не проигрался
                }
            }
        }

        private static string ReadSettingsFromFile(string error, string settingsPath) //Функция чтения файла Settings в классы
        {
            if (!File.Exists(settingsPath) || new FileInfo(settingsPath).Length == 0)
            {
                error += "С последнего запуска приложения настройки не были определены. Пожалуйста, назначьте их.\n";
                return error;
            }
            StreamReader settings = new StreamReader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SSOInformator", "Settings.ini"));
            _connections = new List<Connection>();
            bool hasValidIP = false;
            bool FirstLine = true; // В первой линии содержится запись о задержке подключения
            while (!settings.EndOfStream)
            {
                string line = settings.ReadLine();
                if (line.Length > 0)
                {
                    string[] split = line.Split(' ');
                    if (FirstLine)
                    {
                        try
                        {
                            delayValue = int.Parse(split[0]);
                            if (delayValue < 1 || delayValue > 60)
                            {
                                error += "Значение задержки должно быть не менее 1-ой минуты и не более 60-ти минут. Проверьте настройки.\n";
                                return error;
                            }
                            else
                            {
                                delayValue *= 60000;
                            }
                        }
                        catch (Exception)
                        {
                            error += "Настройки задержки неккоректны. Проверьте настройки.\n";
                            return error;
                        }
                    }
                    else if (split.Length == 3 || split.Length == 2) // Длина 3 - это через пробел айпи,логин,пароль Длина 2 - это через пробел айпи,логин
                    {
                        try
                        {
                            Connection conn = new Connection();
                            conn.IPAddress = split[0];
                            conn.Login = split[1];
                            conn.Password = split[2];
                            _connections.Add(conn);
                            hasValidIP = true;
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
                FirstLine = false;
            }
            settings.Close();
            if (!hasValidIP)
            {
                error += "Обнаружена проблема в настройках IP-адреса/логина/пароля, проверьте настройки.\n";
            }
            return error;
        }
        private void StopButton_Click(object sender, EventArgs e) //Кнопка "Стоп"
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel(); // Запрос отмены потока(Бесконечного цикла в функции StartButton_Click)
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
            MainWindow.Instance.ConsoleTextBox.Text += $"[{DateTime.Now.ToString("HH:mm:ss")}] Выполнение подключения остановлено.\n";
            ChangeTrayIconOnIdle(this, new RoutedEventArgs()); // Смена иконки на обычную(безцветную)
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;

            if (MenuItemAdded) // Удаление "Остановить" из контекстного меню в трее
            {
                App app = Application.Current as App;
                var contextMenu = app._notifyIcon.ContextMenu;

                var stopMenuItem = contextMenu.MenuItems
                    .OfType<System.Windows.Forms.MenuItem>()
                    .FirstOrDefault(item => item.Text == "Остановить");

                if (stopMenuItem != null)
                {
                    contextMenu.MenuItems.Remove(stopMenuItem);
                }

                MenuItemAdded = false; // Устанавливаем флаг в false, чтобы пометить, что кнопка "Остановить" была удалена
            }
            AddStartToContexMenu(this, new RoutedEventArgs()); //Добавление "Запустить" в контекстное меню
        }
        private void AddStartToContexMenu(object sender, RoutedEventArgs e) // Функция добавления "Запустить" в контекстное меню
        {
            App app = Application.Current as App;
            var contextMenu = app._notifyIcon.ContextMenu;

            if (contextMenu != null)
            {
                // Очистить существующее контекстное меню
                contextMenu.MenuItems.Clear();
            }
            else
            {
                // Если контекстное меню не установлено, создать новый экземпляр
                contextMenu = new System.Windows.Forms.ContextMenu();
                app._notifyIcon.ContextMenu = contextMenu;
            }

            // Добавить новые элементы меню
            var startMenuItem = new System.Windows.Forms.MenuItem("Запустить");
            startMenuItem.Click += StartButton_Click;
            contextMenu.MenuItems.Add(startMenuItem);

            var settingsMenuItem = new System.Windows.Forms.MenuItem("Настройки");
            settingsMenuItem.Click += SettingsButton_Click;
            contextMenu.MenuItems.Add(settingsMenuItem);

            var exitMenuItem = new System.Windows.Forms.MenuItem("Выход");
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.MenuItems.Add(exitMenuItem);
        }
        private void AddStopToContexMenu(object sender, RoutedEventArgs e)
        {
            App app = Application.Current as App;
            var contextMenu = app._notifyIcon.ContextMenu;

            if (contextMenu != null)
            {
                // Очистить существующее контекстное меню
                contextMenu.MenuItems.Clear();
            }
            else
            {
                // Если контекстное меню не установлено, создать новый экземпляр
                contextMenu = new System.Windows.Forms.ContextMenu();
                app._notifyIcon.ContextMenu = contextMenu;
            }

            // Добавить новые элементы меню
            var stopMenuItem = new System.Windows.Forms.MenuItem("Остановить");
            stopMenuItem.Click += StopButton_Click;
            contextMenu.MenuItems.Add(stopMenuItem);

            var settingsMenuItem = new System.Windows.Forms.MenuItem("Настройки");
            settingsMenuItem.Click += SettingsButton_Click;
            contextMenu.MenuItems.Add(settingsMenuItem);

            var exitMenuItem = new System.Windows.Forms.MenuItem("Выход");
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.MenuItems.Add(exitMenuItem);
        }
        private void SettingsButton_Click(object sender, EventArgs e) // Обработки кнопки "Настройки"(как в самой программе так и в трее)
        {

            bool isWindowOpen = false;

            foreach (Window w in Application.Current.Windows)
            {
                if (w is SettingsWindow)
                {
                    isWindowOpen = true;
                    w.Activate();
                }
            }
            if (!isWindowOpen)
            {
                WindowState = WindowState.Normal;
                Activate();
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.NewSettings = false;
                settingsWindow.ShowDialog();
                bool result = settingsWindow.NewSettings;
                if (result)
                {
                    if (isRequestInProgress)
                    {
                        SettingsChanged = true;
                    }
                    EventArgs args = new EventArgs();
                    StopButton_Click(this, args);
                    EventArgs args2 = new EventArgs();
                    StartButton_Click(this, args2);
                }
            }
        }
        private void ExitMenuItem_Click(object sender, EventArgs e) // Обработки контестной кнопки "Выход" в трее
        {
            Close();
        }
        private void ChangeTrayIconOnError(object sender, RoutedEventArgs e) // Функция смены иконки в трее на красную
        {
        App app = Application.Current as App;
        System.Drawing.Icon newIcon = new System.Drawing.Icon("Resources/informatorError_icon.ico");
        app._notifyIcon.Icon = newIcon;
        app._notifyIcon.Text = "ОРЭ Информатор. Состояние - Ошибка подключения.";
        Uri newIconUri = new Uri("pack://application:,,,/Resources/informatorError_icon.ico");
        this.Icon = BitmapFrame.Create(newIconUri);
        }
        private void ChangeTrayIconOnStable(object sender, RoutedEventArgs e) // Функция смены иконки в трее на зелёную
        {
            App app = Application.Current as App;
            System.Drawing.Icon newIcon = new System.Drawing.Icon("Resources/informatorStable_icon.ico");
            app._notifyIcon.Icon = newIcon;
            app._notifyIcon.Text = "ОРЭ Информатор. Состояние - Успешно.";
            Uri newIconUri = new Uri("pack://application:,,,/Resources/informatorStable_icon.ico");
            this.Icon = BitmapFrame.Create(newIconUri);
        }

        private void ChangeTrayIconOnIdle(object sender, RoutedEventArgs e) // Функция смены иконки в трее на обычную(безцветную)
        {
            App app = Application.Current as App;
            System.Drawing.Icon newIcon = new System.Drawing.Icon("Resources/informatorIdle_icon.ico");
            app._notifyIcon.Icon = newIcon;
            app._notifyIcon.Text = "ОРЭ Информатор. Состояние - Подключение не запущено.";
            Uri newIconUri = new Uri("pack://application:,,,/Resources/informatorIdle_icon.ico");
            this.Icon = BitmapFrame.Create(newIconUri);
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e) // Функция обработки кнопки "Очистить"
        {
            // Показываем диалоговое окно с подтверждением
            MessageBoxResult result = MessageBox.Show("Вы действительно хотите очистить окно?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Если пользователь нажал "Да", очищаем текстовое поле
                ConsoleTextBox.Text = string.Empty;
            }

        }
        private void GuideButton_Click(object sender, RoutedEventArgs e) // Функция обработки кнопки "Справка"
        {
            infoWindow = new InfoWindow();
            infoWindow.ShowDialog();
        }
    }
}
