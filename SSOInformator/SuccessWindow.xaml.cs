using System.Collections.Generic;
using System.Windows;
using static SSOInformator.MainWindow;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для SuccessWindow.xaml
    /// </summary>
    public partial class SuccessWindow : Window
    {
        public SuccessWindow(List<Mistake> Ipinfo)
        {
            InitializeComponent();

            string errorMessage = "";
            foreach (Mistake IpAdress in Ipinfo)
            {
                errorMessage += "Подключение успешно!\nIP-адрес: " + IpAdress.IPAddress + "\n";
            }
            SucessLabel.Text = errorMessage;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.Close();
        }
    }
}
