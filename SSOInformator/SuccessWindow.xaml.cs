using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using static SSOInformator.MainWindow;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для SuccessWindow.xaml
    /// </summary>
    public partial class SuccessWindow : Window
    {
        private DispatcherTimer timer;
        public SuccessWindow(List<Mistake> Ipinfo)
        {
            InitializeComponent();

            string errorMessage = "";
            foreach (Mistake IpAdress in Ipinfo)
            {
                errorMessage += "Подключение успешно!\nIP-адрес: " + IpAdress.IPAddress + "\n";
            }
            SucessLabel.Text = errorMessage;
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
