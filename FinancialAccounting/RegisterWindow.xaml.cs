using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FinancialAccounting
{
    /// <summary>
    /// Логика взаимодействия для RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }



        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            string hashedPassword = PasswordEncryptor.Encrypt(password);

            try
            {
                using (var db = new DatabaseManager())
                using (var cmd = db.GetOpenConnection().CreateCommand())
                {
                    // Проверка на существующего пользователя
                    cmd.CommandText = "SELECT COUNT(*) FROM users WHERE login = @login OR email = @email";
                    cmd.Parameters.AddWithValue("login", username);
                    cmd.Parameters.AddWithValue("email", email);

                    long userExists = (long)cmd.ExecuteScalar();
                    if (userExists > 0)
                    {
                        MessageBox.Show("Пользователь с таким логином или email уже существует.");
                        return;
                    }

                    // Добавление нового пользователя
                    cmd.CommandText = "INSERT INTO users (login, email, passwordhash) VALUES (@login, @email, @password)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("login", username);
                    cmd.Parameters.AddWithValue("email", email);
                    cmd.Parameters.AddWithValue("password", hashedPassword);

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Регистрация прошла успешно!");

                    // Переход на окно авторизации
                    LoginWindow loginWindow = new LoginWindow();
                    loginWindow.Show();

                    this.Close(); // Закрытие текущего окна
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при регистрации: " + ex.Message);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow(); 
            loginWindow.Show();
            this.Close();
        }
    }
}

