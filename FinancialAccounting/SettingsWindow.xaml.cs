using Npgsql;
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
using System.Windows.Shapes;

namespace FinancialAccounting
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        
        private string _username;
        private int _userId;
        public SettingsWindow(string username)
        {
            InitializeComponent();
            _username = username;
            _userId = GetUserIdByUsername(username);


            LoginTextBox.Text = _username;
            EmailTextBox.Text = GetEmailByLogin(_username);
            LoadAccounts();
        }
        private void LoadAccounts()
        {
            Console.WriteLine("LoadAccounts вызван"); // Для проверки вызова метода
            AccountsListBox.Items.Clear();

            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();

                // Получаем список счетов для текущего пользователя
                using (var command = new NpgsqlCommand("SELECT id, bankname, accountnumber FROM accounts WHERE userid = @userid", connection))
                {
                    command.Parameters.AddWithValue("@userid", _userId); // _userId — ID текущего пользователя

                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Console.WriteLine($"ID: {reader.GetInt32(0)}, BankName: {reader.GetString(1)}, AccountNumber: {reader.GetString(2)}"); // Проверка данных

                        // Добавляем счёт в ListBox
                        AccountsListBox.Items.Add(new Account
                        {
                            Id = reader.GetInt32(0),
                            BankName = reader.GetString(1),
                            AccountNumber = reader.GetString(2)
                        });
                    }
                    reader.Close();
                }
            }
        }
        private string GetEmailByLogin(string username)
        {
            string email = string.Empty;

            // Используем DatabaseManager для подключения к базе данных
            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();

                // Вызов функции get_email_by_login
                using (var command = new NpgsqlCommand("SELECT get_email_by_login(@login)", connection))
                {
                    command.Parameters.AddWithValue("@login", username);

                    var result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        email = result.ToString();
                    }
                }
            }

            return email;
        }
        private bool VerifyOldPassword(string username, string oldPassword)
        {
            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();

                // Получаем зашифрованный пароль из базы данных
                using (var command = new NpgsqlCommand("SELECT passwordhash FROM users WHERE login = @login", connection))
                {
                    command.Parameters.AddWithValue("@login", username);

                    var result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        string encryptedPassword = result.ToString();
                        string decryptedPassword = PasswordEncryptor.Decrypt(encryptedPassword);

                        // Сравниваем расшифрованный пароль с введённым
                        return decryptedPassword == oldPassword;
                    }
                }
            }

            return false;
        }

        private void UpdatePassword(string username, string newPassword)
        {
            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();

                // Шифруем новый пароль
                string encryptedPassword = PasswordEncryptor.Encrypt(newPassword);

                // Обновляем пароль в базе данных
                using (var command = new NpgsqlCommand("UPDATE users SET passwordhash = @passwordhash WHERE login = @login", connection))
                {
                    command.Parameters.AddWithValue("@passwordhash", encryptedPassword);
                    command.Parameters.AddWithValue("@login", username);

                    command.ExecuteNonQuery();
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string oldPassword = OldPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;
            string repeatPassword = RepeatPasswordBox.Password;

            // Проверяем, что новый пароль совпадает с подтверждением
            if (newPassword != repeatPassword)
            {
                MessageBox.Show("Новый пароль и его подтверждение не совпадают.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем старый пароль
            if (!VerifyOldPassword(_username, oldPassword))
            {
                MessageBox.Show("Старый пароль введён неверно.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Обновляем пароль
            UpdatePassword(_username, newPassword);
            MessageBox.Show("Пароль успешно изменён. Пожалуйста, войдите снова.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

            // Переход на окно авторизации
            var loginWindow = new LoginWindow(); // Предполагается, что у вас есть окно авторизации LoginWindow
            loginWindow.Show();

            // Закрываем текущее окно
            this.Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow(_username); // Предполагается, что у вас есть класс MainWindow
            mainWindow.Show();

            // Закрываем текущее окно
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Открываем окно авторизации
            var loginWindow = new LoginWindow(); // Предполагается, что у вас есть класс LoginWindow
            loginWindow.Show();

            // Закрываем текущее окно
            this.Close();
        }

        private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountsListBox.SelectedItem is Account selectedAccount)
            {
                BankNameTextBox.Text = selectedAccount.BankName;
                AccountNumberTextBox.Text = selectedAccount.AccountNumber;
            }
        }

        private void SaveAccountChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedItem is Account selectedAccount)
            {
                string newBankName = BankNameTextBox.Text;
                string newAccountNumber = AccountNumberTextBox.Text;

                using (var dbManager = new DatabaseManager())
                {
                    var connection = dbManager.GetOpenConnection();

                    // Обновляем данные счёта
                    using (var command = new NpgsqlCommand("UPDATE accounts SET bankname = @bankname, accountnumber = @accountnumber WHERE id = @id", connection))
                    {
                        command.Parameters.AddWithValue("@bankname", newBankName);
                        command.Parameters.AddWithValue("@accountnumber", newAccountNumber);
                        command.Parameters.AddWithValue("@id", selectedAccount.Id);

                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Изменения сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем список счетов
                LoadAccounts();
            }
        }
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, что выбрана вкладка "Счета"
            if (e.Source is TabControl && ((TabControl)sender).SelectedItem is TabItem selectedTab && selectedTab.Header.ToString() == "Счета")
            {
                LoadAccounts();
            }
        }
        private int GetUserIdByUsername(string username)
        {
            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();

                // Вызов функции get_userid_by_username
                using (var command = new NpgsqlCommand("SELECT get_user_id(@username)", connection))
                {
                    command.Parameters.AddWithValue("@username", username);

                    var result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }

            throw new Exception("Пользователь не найден.");
        }
    }
    public class Account
    {
        public int Id { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }

        public override string ToString()
        {
            return $"{BankName} ({AccountNumber})";
        }
    }
}
