using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ExcelDataReader;
using Microsoft.Win32;
using Npgsql;
using UglyToad.PdfPig;


namespace FinancialAccounting
{
    /// <summary>
    /// Логика взаимодействия для DataUploadWindow.xaml
    /// </summary>
    public partial class DataUploadWindow : Window
    {
        private readonly int _accountId;
        private readonly string _username;
        private string selectedFilePath;
        public DataUploadWindow(int accountId, string username)
        {
            InitializeComponent();
            _accountId = accountId;
            _username = username;
            
        }
        
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Files|*.pdf|Excel Files|*.xls;*.xlsx;*.xlsm",
                Title = "Выберите PDF выписку или Excel файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                txtFileName.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
            }
        }

        private void Processing_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Выберите файл перед загрузкой!");
                return;
            }

            ObservableCollection<TransactionRecord> transactions = new ObservableCollection<TransactionRecord>();

            // Определяем тип файла по расширению
            string fileExtension = System.IO.Path.GetExtension(selectedFilePath).ToLower();

            if (fileExtension == ".pdf")
            {
                // Обработка PDF файла (как в текущей реализации)
                string rawText = "";
                using (var pdf = PdfDocument.Open(selectedFilePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        rawText += "\n" + page.Text;
                    }
                }
                rawText = rawText.Replace('\u00A0', ' ');

                int idx = rawText.IndexOf("Расшифровка операций");
                if (idx >= 0)
                    rawText = rawText.Substring(idx);

                string[] records = Regex.Split(rawText, @"(?=\d{2}\.\d{2}\.\d{4})")
                                          .Where(r => !string.IsNullOrWhiteSpace(r))
                                          .Select(r => r.Trim())
                                          .ToArray();
                Debug.WriteLine($"🔹 Total records after Regex.Split: {records.Length}");

                for (int i = 0; i < records.Length; i++)
                {
                    string record = records[i];
                    if (record.Contains("Итого по операциям"))
                        continue;
                    if (record.Length < 21)
                        continue;

                    string date = record.Substring(0, 10).Trim();
                    string headerPart = record.Substring(21).Trim();
                    Debug.WriteLine($"Processing record: {record}");
                    Debug.WriteLine($"Original header part: {headerPart}");

                    var mAmount = Regex.Match(headerPart, @"[+\-]?\d{1,3}(?:[ ]\d{3})*,\d{2}");
                    if (mAmount.Success)
                    {
                        int amountIndex = mAmount.Index;
                        string category = headerPart.Substring(0, amountIndex).Trim();
                        string amount = mAmount.Value.Trim();
                        string afterAmount = headerPart.Substring(amountIndex + mAmount.Length).Trim();
                        var mBalance = Regex.Match(afterAmount, @"\d{1,3}(?:[ ]\d{3})*,\d{2}");
                        if (mBalance.Success)
                        {
                            string balance = mBalance.Value.Trim();
                            Debug.WriteLine($"Header Parsed: Date={date}, Category={category}, Amount={amount}, Balance={balance}");
                            transactions.Add(new TransactionRecord
                            {
                                Date = date,
                                Category = category,
                                Amount = amount,
                                Balance = balance,
                                Description = ""
                            });
                        }
                        else
                        {
                            Debug.WriteLine("Balance not found for record: " + record);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Amount not found for record (treat as description): " + record);
                        string desc = "";
                        if (record.Length > 10)
                            desc = record.Substring(10).Trim();

                        // Ограничиваем описание до номера карты или ключевых слов
                        var descMatch = Regex.Match(desc, @"^(.*?\*{4}\d{4})");
                        if (descMatch.Success)
                        {
                            desc = descMatch.Groups[1].Value.Trim();
                        }
                        else
                        {
                            // Если номер карты не найден, обрезаем описание до ключевых слов
                            string[] stopWords = { "ПАО Сбербанк", "Генеральная лицензия", "Денежные средства" };
                            foreach (var stopWord in stopWords)
                            {
                                int stopIndex = desc.IndexOf(stopWord, StringComparison.OrdinalIgnoreCase);
                                if (stopIndex > 0)
                                {
                                    desc = desc.Substring(0, stopIndex).Trim();
                                    break;
                                }
                            }
                        }

                        if (transactions.Any())
                        {
                            transactions.Last().Description += " " + desc;
                            transactions.Last().Description = transactions.Last().Description.Trim();
                            Debug.WriteLine($"Updated transaction on {transactions.Last().Date} with description: {transactions.Last().Description}");
                        }
                        else
                        {
                            Debug.WriteLine("No previous transaction to attach description to.");
                        }
                    }
                }
            }
            else if (fileExtension == ".xls" || fileExtension == ".xlsx" || fileExtension == ".xlsm")
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = System.IO.File.Open(selectedFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet();
                        if (result.Tables.Count > 0)
                        {
                            var table = result.Tables[0];

                            for (int i = 1; i < table.Rows.Count; i++)
                            {
                                var row = table.Rows[i];
                                if (row.ItemArray.All(cell => cell == null || string.IsNullOrWhiteSpace(cell.ToString())))
                                    continue;

                                string rawDate = row[0]?.ToString() ?? "";
                                string date = DateTime.TryParse(rawDate, out DateTime dt) ? dt.ToString("dd.MM.yyyy") : rawDate.Split(' ')[0];

                                string category = row[9]?.ToString() ?? "";
                                string rawAmount = row[4]?.ToString() ?? "";
                                string description = row[11]?.ToString() ?? "";

                                decimal amountValue = 0;
                                string type = "Expense"; // По умолчанию считаем, что это расход
                                if (decimal.TryParse(rawAmount, NumberStyles.Number, new CultureInfo("ru-RU"), out amountValue))
                                {
                                    if (amountValue > 0)
                                    {
                                        type = "Income";
                                        rawAmount = "+" + amountValue.ToString("N2", new CultureInfo("ru-RU")); // Добавляем знак "+" для положительных чисел
                                    }
                                    else
                                    {
                                        rawAmount = amountValue.ToString("N2", new CultureInfo("ru-RU")); // Оставляем отрицательные числа как есть
                                    }
                                }

                                transactions.Add(new TransactionRecord
                                {
                                    Date = date,
                                    Category = category,
                                    Amount = rawAmount,
                                    Description = description,
                                    Balance = "",
                                    Type = type
                                });
                            }
                        }
                        else
                        {
                            MessageBox.Show("Excel файл не содержит листов с данными.");
                        }
                    }
                }
            }

            else
            {
                MessageBox.Show("Неподдерживаемый формат файла.");
                return;
            }

            Debug.WriteLine("Total transactions: " + transactions.Count);
            TransactionsGrid.ItemsSource = transactions;
        }

        private void SaveDatabase_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<TransactionRecord> transactions = TransactionsGrid.ItemsSource as ObservableCollection<TransactionRecord>;
            if (transactions == null || transactions.Count == 0)
            {
                MessageBox.Show("Нет данных для сохранения.");
                return;
            }

            using (var db = new DatabaseManager())
            {
                var connection = db.GetOpenConnection();

                // Получаем userid
                int userId;
                using (var userIdCmd = new NpgsqlCommand("SELECT get_user_id(@username)", connection))
                {
                    userIdCmd.Parameters.AddWithValue("@username", _username);
                    userId = Convert.ToInt32(userIdCmd.ExecuteScalar());
                }

                foreach (var transaction in transactions)
                {
                    int categoryId;
                    // Проверяем категорию с учетом userid
                    using (var checkCmd = new NpgsqlCommand(
                        "SELECT id FROM categories WHERE name = @name AND userid = @userid LIMIT 1",
                        connection))
                    {
                        checkCmd.Parameters.AddWithValue("@name", transaction.Category);
                        checkCmd.Parameters.AddWithValue("@userid", userId);
                        var result = checkCmd.ExecuteScalar();

                        if (result != null)
                        {
                            categoryId = Convert.ToInt32(result);
                        }
                        else
                        {
                            // Добавляем категорию с userid
                            using (var insertCategoryCmd = new NpgsqlCommand(
                                "INSERT INTO categories (name, userid) VALUES (@name, @userid) RETURNING id",
                                connection))
                            {
                                insertCategoryCmd.Parameters.AddWithValue("@name", transaction.Category);
                                insertCategoryCmd.Parameters.AddWithValue("@userid", userId);
                                categoryId = Convert.ToInt32(insertCategoryCmd.ExecuteScalar());
                            }
                        }
                    }

                    // Парсинг даты и суммы (остается без изменений)
                    DateTime dt;
                    if (!DateTime.TryParseExact(transaction.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    {
                        dt = DateTime.Now;
                    }

                    string rawAmount = transaction.Amount.Trim();
                    decimal amountValue = 0;
                    decimal.TryParse(rawAmount, NumberStyles.Number, new CultureInfo("ru-RU"), out amountValue);
                    string typeValue = rawAmount.StartsWith("+") ? "Income" : "Expense";

                    using (var duplicateCheckCmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM transactions 
                WHERE date = @date 
                AND amount = @amount 
                AND description = @description 
                AND categoryid = @categoryid
                AND userid = @userid
                AND accountid = @accountid", connection))
                    {
                        duplicateCheckCmd.Parameters.AddWithValue("@date", dt);
                        duplicateCheckCmd.Parameters.AddWithValue("@amount", amountValue);
                        duplicateCheckCmd.Parameters.AddWithValue("@description", transaction.Description ?? "");
                        duplicateCheckCmd.Parameters.AddWithValue("@categoryid", categoryId);
                        duplicateCheckCmd.Parameters.AddWithValue("@userid", userId);
                        duplicateCheckCmd.Parameters.AddWithValue("@accountid", _accountId);

                        int count = Convert.ToInt32(duplicateCheckCmd.ExecuteScalar());
                        if (count > 0) continue;
                    }

                    // Вставка транзакции с userid и accountid
                    using (var insertTransactionCmd = new NpgsqlCommand(@"
                INSERT INTO transactions 
                (date, amount, type, categoryid, description, userid, accountid)
                VALUES 
                (@date, @amount, @type, @categoryid, @description, @userid, @accountid)", connection))
                    {
                        insertTransactionCmd.Parameters.AddWithValue("@date", dt);
                        insertTransactionCmd.Parameters.AddWithValue("@amount", amountValue);
                        insertTransactionCmd.Parameters.AddWithValue("@type", typeValue);
                        insertTransactionCmd.Parameters.AddWithValue("@categoryid", categoryId);
                        insertTransactionCmd.Parameters.AddWithValue("@description", transaction.Description ?? "");
                        insertTransactionCmd.Parameters.AddWithValue("@userid", userId);
                        insertTransactionCmd.Parameters.AddWithValue("@accountid", _accountId);

                        insertTransactionCmd.ExecuteNonQuery();
                    }
                }
            }

            MessageBox.Show("Транзакции успешно сохранены в базу данных!");
        }
        private void ApplyFilter()
        {
            if (TransactionsGrid.ItemsSource is ObservableCollection<TransactionRecord> originalList)
            {
                var filteredList = originalList.AsEnumerable();

                // Фильтрация по категории
                if (CategoryComboBox.SelectedItem is ComboBoxItem selectedCategoryItem)
                {
                    string selectedCategory = selectedCategoryItem.Content.ToString();
                    if (selectedCategory != "Все категории")
                    {
                        filteredList = filteredList.Where(r => r.Category == selectedCategory);
                    }
                }

                // Фильтрация по диапазону дат
                if (StartDatePicker.SelectedDate.HasValue)
                {
                    filteredList = filteredList.Where(r =>
                    {
                        if (DateTime.TryParse(r.Date, out DateTime date))
                            return date >= StartDatePicker.SelectedDate.Value;
                        return false;
                    });
                }

                if (EndDatePicker.SelectedDate.HasValue)
                {
                    filteredList = filteredList.Where(r =>
                    {
                        if (DateTime.TryParse(r.Date, out DateTime date))
                            return date <= EndDatePicker.SelectedDate.Value;
                        return false;
                    });
                }

                TransactionsGrid.ItemsSource = new ObservableCollection<TransactionRecord>(filteredList);
            }
        }

        private void FilterDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TransactionsGrid != null)
            {
                ApplyFilter();
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TransactionsGrid != null)
            {
                ApplyFilter();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (TransactionsGrid != null)
            {
                TransactionsGrid.ItemsSource = null; 
                TransactionsGrid.Items.Clear();      
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow(_username); 
            mainWindow.Show();
            this.Close();
        }
    }


    public class TransactionRecord
    {
        public string Date { get; set; }
        public string Category { get; set; }
        public string Amount { get; set; }
        public string Balance { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
}