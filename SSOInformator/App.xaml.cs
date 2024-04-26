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

        private App()
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
                ((MainWindow)Current.MainWindow).isAppAlreadyRunning = true;
            }

            _notifyIcon.Icon = new System.Drawing.Icon("Resources/informatorIdle_icon.ico");
            _notifyIcon.Text = "ОРЭ Информатор. Состояние - Подключение не запущено.";      // Задание первоначального текста при наведении и иконки в трее
            _notifyIcon.Click += NotifyIcon_Click;
            _notifyIcon.Visible = true;

            base.OnStartup(e);
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
