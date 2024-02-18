using System;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Threading;


namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Forms.NotifyIcon _notifyIcon; // Для иконки в трее

        public App()
        {
            _notifyIcon = new Forms.NotifyIcon(); //для иконки в трее
        }
        private Mutex _mutex; // Мьютекс нужен для запрета повторного запуска приложения
        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, "MyAppMutex", out createdNew);

            if (!createdNew)
            { // Приложение уже запущено
                MessageBox.Show("Приложение уже запущено! Проверьте иконку в трее Windows.",
                                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                // Устанавливаем флаг, указывающий, что есть окно "Приложение уже запущено" дабы избежать дальнейшего окна "Вы хотите закрыть приложение?"
                ((MainWindow)Current.MainWindow).isAppAlreadyRunning = true; //КОСТЫЛЬ ВЫХОДА!!!!!

                Current.Shutdown();//сюда не доходит
            }

            _notifyIcon.Icon = new System.Drawing.Icon("Resources/informatorIdle_icon.ico");
            _notifyIcon.Text = "ОРЭ Информатор. Состояние - Подключение не запущено.";      // Задание первоначального текста при наведении и иконки в трее
            _notifyIcon.Click += NotifyIcon_Click;

            var exitMenuItem = new System.Windows.Forms.MenuItem("Выход");
            exitMenuItem.Click += ExitMenuItem_Click;
            var settingsMenuItem = new System.Windows.Forms.MenuItem("Настройки");      // Задание кнопок контекстного меню для иконки в трее
            settingsMenuItem.Click += SettingsMenuItem_Click;

            var contextMenu = new System.Windows.Forms.ContextMenu();
            contextMenu.MenuItems.Add(settingsMenuItem);                                
            contextMenu.MenuItems.Add(exitMenuItem);

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.Visible = true;

            base.OnStartup(e);
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
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            }
        }
        private void ExitMenuItem_Click(object sender, EventArgs e) // Обработки контестной кнопки "Выход" в трее
        {
            MainWindow.Close();
        }
        private void NotifyIcon_Click(object sender, EventArgs e) // Обработка разворачивания окна при клике по иконке в трее
        {
            var mouseEvent = e as Forms.MouseEventArgs;

            if (mouseEvent != null && mouseEvent.Button == Forms.MouseButtons.Left) // Условие If нужно чтобы приложение разворачивалось только тогда когда была нажата ЛКМ по иконке
            {                                                                       // Если был выбран элемент из контекстного меню то разворачиваться оно не будет
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        }
        protected override void OnExit(ExitEventArgs e) // Для правильного закрытия в отношении Мьютекста
        {
            _mutex?.ReleaseMutex();
            _mutex?.Close();
            _notifyIcon.Dispose();
            base.OnExit(e);
        }
    }
}
