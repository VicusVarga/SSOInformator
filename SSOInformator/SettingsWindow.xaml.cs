using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SSOInformator
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        string settingsPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName, "Resources", "Settings.ini"); // Путь к файлу Settings 
        public SettingsWindow()
        {
            InitializeComponent();
            if (File.Exists(settingsPath)) //если файл существует читаем его
            {
                string[] settings = File.ReadAllLines(settingsPath);

                if (settings.Length == 2)
                {
                    string[] values = settings[1].Split(' ');
                    if (values.Length == 3)
                    {
                        DelayTextBox.Text = settings[0];             //  
                        IPTextBox.Text = values[0];                  // Запись данных по текстбоксам
                        LoginTextBox.Text = values[1];               //
                        PasswordTextBox.Text = values[2];            //
                    }
                }
            }
            else
            {
                File.WriteAllText(settingsPath, ""); // Если файла нет создаём его пустым
            }
        }
        private void CanceButton_Click(object sender, RoutedEventArgs e) // Кнопка "отмена"
        {
            Window window = Window.GetWindow(this);
            window.Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e) // Кнопка "ок"
        {
            string delayValue = DelayTextBox.Text;
            int.TryParse(delayValue, out int delay);
            string ipValue = IPTextBox.Text;
            string loginValue = LoginTextBox.Text;
            string passwordValue = PasswordTextBox.Text;
            if (string.IsNullOrEmpty(ipValue) || string.IsNullOrEmpty(loginValue) || string.IsNullOrEmpty(delayValue)) // Если нет пустых текстбоксов(исключение - текстбокс пароля)
            {
                MessageBox.Show("Все поля настроек должны быть заполнены. Пустым может быть только пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (delay < 5000)
            {
                MessageBox.Show("Значение задержки должно быть больше или равно 5000.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); // Значение задержки меньше 5000
                return;
            }
            File.WriteAllText(settingsPath, ""); //очищение старых данных файла Settings
            File.AppendAllText(settingsPath, delayValue + Environment.NewLine);                                // Запись данных в файл        
            File.AppendAllText(settingsPath, $"{ipValue} {loginValue} {passwordValue}" + Environment.NewLine); //
            string message = "Настройки сохранены. Если программа уже работает, перезапустите подключение, чтобы использовать последние настройки.";
            MessageBox.Show(message, "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            Window window = Window.GetWindow(this);
            window.Close();
            
        }

        private void DelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) // Запрет ввода чего либо кроме цифр в текстбокс задержки
        {
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
            {
                e.Handled = true;
            }
        }

        private void DelayTextBox_PreviewKeyDown(object sender, KeyEventArgs e) // Запрет ввода пробела в текстбокс задержки
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void IPTextBox_PreviewKeyDown(object sender, KeyEventArgs e) // Запрет ввода пробела в текстбокс Ip-адреса
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void LoginTextBox_PreviewKeyDown(object sender, KeyEventArgs e) // Запрет ввода пробела в текстбокс логина
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void PasswordTextBox_PreviewKeyDown(object sender, KeyEventArgs e) // Запрет ввода пробела в текстбокс пароля
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
    }
}
