using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using static SSOInformator.MainWindow;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        private DispatcherTimer timer;
        public ErrorWindow(List<Mistake> mistakes)
        {
            InitializeComponent();

            string errorMessage = "";
            foreach (Mistake mistake in mistakes)
            {
                errorMessage += "IP-адрес: " + mistake.IPAddress + "\n";
                errorMessage += "Причина ошибки: " + mistake.typeofmistake + "\n\n";
            }
            ErrorLabel.Text = errorMessage;

            // Таймер автоматического закрытия окна
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(30);
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.Close();
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Закрываем окно
            Close();
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Обязательно останавливаем таймер при закрытии окна, чтобы избежать утечек памяти
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }
    }
}
