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

        private CancellationTokenSource cancellationTokenSource; // для кнопки "стоп", для отмены потока моментально после нажатия "стоп"(чтобы из-за задержки не багавало)
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
            base.OnClosing(e);
        }
        public class Connection // Класс Подключений со сведением о подключаемом сервере.
        {
            public string IPAddress { set; get; }
            public string Login { set; get; }
            public string Password { set; get; }
            public bool notfall { set; get; } // булевое поле которое будем использовать чтобы понимать было ли подключение в прошлом цикле удачным. Нужно для отображения окна ошибки/успеха без спама.
        }
        public class Mistake  // Класс для записи "Ip адрес-ошибка"
        {
            public string IPAddress { set; get; }
            public string typeofmistake { set; get; }
        }
        private async void StartButton_Click(object sender, EventArgs e) //Кнопка "Старт"
        {
            StopButton.IsEnabled = true; // Активация кнопки "стоп"
            StartButton.IsEnabled = false; // Блок кнопки "старт"
            string settingsPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName, "Resources", "Settings.ini"); // Путь к файлу Settings
            bool NoSettings = CheckFiles(settingsPath); //вызов функции поиска settings.txt
            if (!NoSettings) // обработка если была проблема с settings.txt
            {
                string message = "С последнего запуска приложения настройки не были определены. Пожалуйста, назначьте их";
                MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                StartButton.IsEnabled = true;
                StopButton.IsEnabled= false;
                WindowState = WindowState.Normal;
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                AddStartToContexMenu(this, new RoutedEventArgs());
                return;
            }
            string error = "";
            error = ReadSettingsFromFile(error); //Вызов функции записи данных из Settings.txt в классы

            if (!string.IsNullOrEmpty(error)) // обработка если была проблема с settings.txt
            {
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();                            // Открытия окна с настройками
                AddStartToContexMenu(this, new RoutedEventArgs());
                return;
            }
            List<Mistake> mistakes = new List<Mistake>(); //создание списка для записи айпишников неудачных подключений и видов ошибок для дальнейшей отправки в одно письмо
            cancellationTokenSource = new CancellationTokenSource(); // для отмены потока моментально после нажатия "стоп"(чтобы из-за задержки не багавало)
 
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

            while (true) // Беск.цикл  подключающийся к ip-адресам и отправляющий сообщение ошибки/успеха
            {            // сообщение ошибки показывается в том случае если это первый сбой в подключении к IP. Т.е. подключение к этому IP-адресу в прошлом цикле было успешным.
                         // аналогично с сообщением об успехе
                foreach (Connection conn in _connections)
                {
                    try
                    {
                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + conn.IPAddress);   // Само подключение к серверу
                        request.Credentials = new NetworkCredential(conn.Login, conn.Password);                //
                        request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;                           //
                        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())                // Если будет проблема с подключением тут вызовется исключение и переход на блок catch

                        MainWindow.Instance.ConsoleTextBox.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "]" + "Подключение к IP: " + conn.IPAddress + " выполнено успешно!"; // Вывод на текстбокс в программе
                        ChangeTrayIconOnStable(this, new RoutedEventArgs()); // Смена иконки в трее на зелёную
                        if (conn.notfall == false) // Если сервер был в ауте в прошлом цикле, а сейчас удалось подключится показываем окно успеха
                            {
                                Mistake mist = new Mistake(); // Айпи берём из класса об ошибках
                                mist.IPAddress = conn.IPAddress;
                                mistakes.Add(mist);
                                SuccessWindow successWindow = new SuccessWindow(mistakes);
                                successWindow.Topmost = true;
                                successWindow.Show();
                                try
                                {
                                    SoundPlayer Ipinfosound = new SoundPlayer("Resources/success_sound.wav");
                                    Ipinfosound.Play();
                                }
                                catch (Exception)
                                {

                                }
                            }
                            conn.notfall = true; // Обозначаем текущее состояние сервера как стабильное.
                    }

                    catch (WebException ex) // Вызывается если не удалось подключиться
                    {
                        MainWindow.Instance.ConsoleTextBox.Text += "[" + DateTime.Now.ToString("HH:mm:ss") + "]" + "Ошибка подключения к IP " + conn.IPAddress + " Ошибка: " + ex.Message; // Вывод на текстбокс в программе
                        ChangeTrayIconOnError(this, new RoutedEventArgs()); // Смена иконки в трее на красную 
                        if (conn.notfall == true) // Если подключение было успешным в прошлом цикле, а сейчас не удалось подключится показываем окно ошибки
                        {
                            conn.notfall = false;
                            Mistake mist = new Mistake();
                            mist.IPAddress = conn.IPAddress;
                            mist.typeofmistake = ex.Message;
                            mistakes.Add(mist);
                            ErrorWindow errorWindow = new ErrorWindow(mistakes);
                            errorWindow.Topmost = true;
                            errorWindow.Show();
                            try
                            {
                                SoundPlayer mistakesound = new SoundPlayer("Resources/error_sound.wav");
                                mistakesound.Play();
                            }
                            catch (Exception)
                            {

                            }
                        }

                    }
                    MainWindow.Instance.ConsoleTextBox.Text += "\n";
                }
                mistakes.Clear();   //очищаем весь класс проблемных айпишников для след. цикла
                try
                {
                    await Task.Delay(delayValue, cancellationTokenSource.Token); // Задержка с мониторингом отмены потока
                }
                catch (TaskCanceledException)
                {
                    // Если отменили поток(нажали кнопку "стоп")
                    return; // Завершить всю функцию кнопки "старт"
                }
            }
        }
        private static bool CheckFiles(string settingsPath) //функция проверки наличия файла settings и записей в нём
        {
            if (!File.Exists(settingsPath) || new FileInfo(settingsPath).Length == 0)
            {
                return false;
            }
            return true;
        }
        private static string ReadSettingsFromFile(string error) //Функция чтения файла Settings в классы
        {
            StreamReader settings = new StreamReader(Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName + "/Resources/Settings.ini"));
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
                            if (delayValue < 5000)
                            {
                                error += "Задержка меньше 5000 мс недопустима. Проверьте настройки.\n";
                                return error;
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
                            conn.notfall = true;
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
        private void StopButton_Click(object sender, EventArgs e)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel(); // Запрос отмены потока(Бесконечного цикла в функции StartButton_Click)
                cancellationTokenSource = null;
                MainWindow.Instance.ConsoleTextBox.Text += $"[{DateTime.Now.ToString("HH:mm:ss")}] Выполнение программы остановлено.\n";
            }
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
            settingsMenuItem.Click += SettingsMenuItem_Click;
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
            settingsMenuItem.Click += SettingsMenuItem_Click;
            contextMenu.MenuItems.Add(settingsMenuItem);

            var exitMenuItem = new System.Windows.Forms.MenuItem("Выход");
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.MenuItems.Add(exitMenuItem);
        }
        private void SettingsMenuItem_Click(object sender, EventArgs e) // Обработки контестной кнопки "Настройки" в трее
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
                settingsWindow.ShowDialog();
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e) // Функция обработки кнопки "Настройки"
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) // Функция обработки кнопки "Выход"
        {
            Close();
        }
        private void GuideButton_Click(object sender, RoutedEventArgs e) // Функция обработки кнопки "Справка"
        {
            infoWindow = new InfoWindow();
            infoWindow.ShowDialog();
        }
    }
}
