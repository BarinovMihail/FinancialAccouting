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
using System.Collections.Generic;
using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
namespace FinancialAccounting
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _username;
        public MainWindow(string username)
        {
            InitializeComponent();
            _username = username;
            UsernameTextBlock.Text = $"Пользователь: {_username}";
            LoadAccounts(_username);

        }
        private void LoadCharts(int userId, int accountId)
        {
            var expenseData = new Dictionary<string, double>();
            var incomeData = new Dictionary<string, double>();

            using (var db = new DatabaseManager())
            {
                var conn = db.GetOpenConnection();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT c.name AS category, t.amount, t.type
                FROM transactions t
                LEFT JOIN categories c ON t.categoryid = c.id
                WHERE t.userid = @userId AND t.accountid = @accountId;
            ";

                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("accountId", accountId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string category = reader["category"] as string ?? "Без категории";
                            double amount = Convert.ToDouble(reader["amount"]);
                            string type = reader["type"].ToString().ToLower();

                            if (type == "expense")
                            {
                                if (expenseData.ContainsKey(category))
                                    expenseData[category] += amount;
                                else
                                    expenseData[category] = amount;
                            }
                            else if (type == "income")
                            {
                                if (incomeData.ContainsKey(category))
                                    incomeData[category] += amount;
                                else
                                    incomeData[category] = amount;
                            }
                        }
                    }
                }
            }

            // Обновляем график расходов
            ExpenseChart.Series = new SeriesCollection();
            foreach (var kvp in expenseData)
            {
                ExpenseChart.Series.Add(new PieSeries
                {
                    Title = kvp.Key,
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true
                });
            }

            // Обновляем график доходов
            IncomeChart.Series = new SeriesCollection();
            foreach (var kvp in incomeData)
            {
                IncomeChart.Series.Add(new PieSeries
                {
                    Title = kvp.Key,
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true
                });
            }
        }

        private void LoadAccounts(string username)
        {
            try
            {
                List<AccountInfo> accounts = new List<AccountInfo>();

                using (var db = new DatabaseManager())
                using (var cmd = db.GetOpenConnection().CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT id, bankname, accountnumber
                FROM accounts
                WHERE userid = get_user_id(@username)";

                    cmd.Parameters.AddWithValue("username", username);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string bankName = reader.GetString(1);
                            string accountNumber = reader.GetString(2);
                            string last4 = accountNumber.Length >= 4 ? accountNumber.Substring(accountNumber.Length - 4) : "XXXX";

                            accounts.Add(new AccountInfo
                            {
                                Id = reader.GetInt32(0),
                                DisplayName = $"{bankName} ••••{last4}"
                            });
                        }
                    }
                }

                AccountComboBox.ItemsSource = accounts;
                AccountComboBox.DisplayMemberPath = "DisplayName";
                AccountComboBox.SelectedValuePath = "Id";

                if (accounts.Count > 0)
                    AccountComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке счетов: " + ex.Message);
            }
        }


        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            var addAccountWindow = new AddAccountWindow(_username);
            addAccountWindow.Show();    
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow(); 
            loginWindow.Show();
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (AccountComboBox.SelectedItem is AccountInfo selectedAccount)
            {
                int accountId = selectedAccount.Id;

                var uploadWindow = new DataUploadWindow(accountId, _username);
                uploadWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите счёт.");
            }
        }

        private void AccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountComboBox.SelectedItem is AccountInfo selectedAccount)
            {
                int userId = GetUserIdByUsername(_username);
                int accountId = selectedAccount.Id;

                if (userId > 0 && accountId > 0)
                {
                    LoadCharts(userId, accountId);
                }
                else
                {
                    MessageBox.Show("Ошибка при определении пользователя или счёта.");
                }
            }
        }

        private int GetUserIdByUsername(string username)
        {
            try
            {
                using (var db = new DatabaseManager())
                using (var cmd = db.GetOpenConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT get_user_id(@username)";
                    cmd.Parameters.AddWithValue("username", username);

                    object result = cmd.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int userId))
                    {
                        return userId;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при получении userId: " + ex.Message);
            }

            return -1; // Возврат -1 если не удалось получить userId
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (AccountComboBox.SelectedItem is AccountInfo selectedAccount)
            {
                int userId = GetUserIdByUsername(_username);
                int accountId = selectedAccount.Id;
                var reportsWindow = new ReportsWindow(accountId, userId);
                reportsWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите счёт.");
            }
            
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (AccountComboBox.SelectedItem is AccountInfo selectedAccount)
            {
                int accountId = selectedAccount.Id;
                var analyticsWindow = new AnalyticsWindow(accountId);
                analyticsWindow.Show();              
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите счёт.");
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
           
    
                var settingsWindow = new SettingsWindow(_username);
                settingsWindow.Show();
                this.Close();
            
        }
    }
}
public class AccountInfo
{
    public int Id { get; set; }
    public string DisplayName { get; set; } // Название для ComboBox
}

