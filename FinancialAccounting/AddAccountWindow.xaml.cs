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
using static UglyToad.PdfPig.Core.PdfSubpath;

namespace FinancialAccounting
{
    
    
    public partial class AddAccountWindow : Window
    {
        private string _username;

        public AddAccountWindow(string username)
        {
            InitializeComponent();
            _username = username;
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
        
                int userId;

                using (var db = new DatabaseManager())
                using (var cmd = db.GetOpenConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT get_user_id(@username)";
                    cmd.Parameters.AddWithValue("username", _username); // переменная логина текущего пользователя

                    object result = cmd.ExecuteScalar();
                    if (result == null || !int.TryParse(result.ToString(), out userId))
                    {
                        MessageBox.Show("Не удалось получить ID пользователя.");
                        return;
                    }
                }

      

        string cardNumber = CardNumberBox.Text.Trim();
        string bankName = BankNameBox.Text.Trim();

    if (string.IsNullOrEmpty(cardNumber) || string.IsNullOrEmpty(bankName))
    {
        MessageBox.Show("Пожалуйста, заполните все поля.");
        return;
    }

    try
    {
        using (var db = new DatabaseManager())
        using (var cmd = db.GetOpenConnection().CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO accounts (userid, bankname, accountnumber, balance)
                VALUES (@userid, @bankname, @accountnumber, @balance);
            ";

            cmd.Parameters.AddWithValue("userid", userId);
            cmd.Parameters.AddWithValue("bankname", bankName);
            cmd.Parameters.AddWithValue("accountnumber", cardNumber);
            cmd.Parameters.AddWithValue("balance", 0); // Можно сделать поле ввода, если нужно

            cmd.ExecuteNonQuery();

            MessageBox.Show("Счет успешно добавлен!");
                    var mainWindow = new MainWindow(_username);
                    mainWindow.Show();
                    this.Close();
                    
    }
}
    catch (Exception ex)
    {
    MessageBox.Show("Ошибка при добавлении счета: " + ex.Message);
}
}


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void CardNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем ввод только цифр
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void CardNumberBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Убираем обработчик, чтобы избежать рекурсии
            CardNumberBox.TextChanged -= CardNumberBox_TextChanged;

            // Удаляем все пробелы
            string text = CardNumberBox.Text.Replace(" ", "");

            // Форматируем текст в виде "XXXX XXXX XXXX XXXX"
            StringBuilder formattedText = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    formattedText.Append(" ");
                }
                formattedText.Append(text[i]);
            }

            // Устанавливаем отформатированный текст
            CardNumberBox.Text = formattedText.ToString();

            // Перемещаем курсор в конец
            CardNumberBox.CaretIndex = CardNumberBox.Text.Length;

            // Восстанавливаем обработчик
            CardNumberBox.TextChanged += CardNumberBox_TextChanged;
        }
    }
}
