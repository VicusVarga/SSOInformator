using System;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;


namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Forms.NotifyIcon _notifyIcon; // Для иконки в трее

        [DllImport("user32", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string cls, string win);
        [DllImport("user32")]
        static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32")]
        static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32")]
        static extern bool OpenIcon(IntPtr hWnd);
        private App()
        {
            _notifyIcon = new Forms.NotifyIcon(); //для иконки в трее
        }
        private Mutex _mutex;
        protected override void OnStartup(StartupEventArgs e)
        {
            bool isNew;
            _mutex = new Mutex(true, "My Singleton Instance", out isNew);
            if (!isNew)
            {
                ActivateOtherWindow();
                Environment.Exit(0);
            }

            _notifyIcon.Icon = new System.Drawing.Icon("Resources/informatorIdle_icon.ico");
            _notifyIcon.Text = "ОРЭ Информатор. Состояние - Подключение не запущено.";      // Задание первоначального текста при наведении и иконки в трее
            _notifyIcon.Click += NotifyIcon_Click;
            _notifyIcon.Visible = true;

            base.OnStartup(e);
        }
        private static void ActivateOtherWindow()
        {
            var other = FindWindow(null, "ОРЭ Информатор");
            if (other != IntPtr.Zero)
            {
                SetForegroundWindow(other);
                if (IsIconic(other))
                    OpenIcon(other);
            }
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
