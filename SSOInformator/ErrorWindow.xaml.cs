using System.Collections.Generic;
using System.Windows;
using static SSOInformator.MainWindow;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
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
        }
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            window.Close();
        }
    }
}
