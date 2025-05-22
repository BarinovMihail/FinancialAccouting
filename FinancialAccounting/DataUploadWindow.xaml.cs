using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        private async void Processing_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Выберите файл перед загрузкой!");
                return;
            }

            ProgressBar.Visibility = Visibility.Visible;
            ProgressText.Text = "Обработка файла...";
            ProgressBar.Value = 10;

            await Task.Delay(100); 

            ObservableCollection<TransactionRecord> transactions = new ObservableCollection<TransactionRecord>();
            string fileExtension = System.IO.Path.GetExtension(selectedFilePath).ToLower();

            try
            {
                if (fileExtension == ".pdf")
                {
                    string rawText = "";
                    using (var pdf = PdfDocument.Open(selectedFilePath))
                    {
                        int pageCount = pdf.NumberOfPages;
                        int currentPage = 0;

                        foreach (var page in pdf.GetPages())
                        {
                            rawText += "\n" + page.Text;
                            currentPage++;
                            ProgressBar.Value = 10 + (currentPage * 80 / pageCount);
                        }
                    }

                    transactions = PdfParser.ParsePdfText(rawText);
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
                                int totalRows = table.Rows.Count;
                                int processedRows = 0;

                                for (int i = 1; i < totalRows; i++)
                                {
                                    var row = table.Rows[i];
                                    processedRows++;
                                    ProgressBar.Value = 10 + (processedRows * 80 / totalRows);

                                    if (row.ItemArray.All(cell => cell == null || string.IsNullOrWhiteSpace(cell.ToString())))
                                        continue;

                                    string rawDate = row[0]?.ToString() ?? "";
                                    string date = DateTime.TryParse(rawDate, out DateTime dt) ? dt.ToString("dd.MM.yyyy") : rawDate.Split(' ')[0];

                                    string category = row[9]?.ToString() ?? "";
                                    string rawAmount = row[4]?.ToString() ?? "";
                                    string description = row[11]?.ToString() ?? "";

                                    decimal amountValue = 0;
                                    string type = "Expense";
                                    if (decimal.TryParse(rawAmount, NumberStyles.Number, new CultureInfo("ru-RU"), out amountValue))
                                    {
                                        if (amountValue > 0)
                                        {
                                            type = "Income";
                                            rawAmount = "+" + amountValue.ToString("N2", new CultureInfo("ru-RU"));
                                        }
                                        else
                                        {
                                            rawAmount = amountValue.ToString("N2", new CultureInfo("ru-RU"));
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
                else if (fileExtension == ".ofx")
                {
                    transactions = OfxParser.ParseOfx(selectedFilePath);
                }
                else
                {
                    MessageBox.Show("Неподдерживаемый формат файла.");
                    return;
                }

                ProgressBar.Value = 100;
                ProgressText.Text = "Обработка завершена.";
                await Task.Delay(500); // чтобы пользователь успел увидеть прогресс
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке файла: {ex.Message}");
            }
            finally
            {
                TransactionsGrid.ItemsSource = transactions;
                await Task.Delay(300);
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Text = "";
            }
        }


        private async void SaveDatabase_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<TransactionRecord> transactions = TransactionsGrid.ItemsSource as ObservableCollection<TransactionRecord>;
            if (transactions == null || transactions.Count == 0)
            {
                MessageBox.Show("Нет данных для сохранения.");
                return;
            }

            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressText.Text = "Сохранение транзакций...";

            await Task.Run(() =>
            {
                using (var db = new DatabaseManager())
                {
                    var connection = db.GetOpenConnection();
                    int userId;
                    using (var userIdCmd = new NpgsqlCommand("SELECT get_user_id(@username)", connection))
                    {
                        userIdCmd.Parameters.AddWithValue("@username", _username);
                        userId = Convert.ToInt32(userIdCmd.ExecuteScalar());
                    }

                    int total = transactions.Count;
                    int current = 0;

                    foreach (var transaction in transactions)
                    {
                        int categoryId;
                        using (var checkCmd = new NpgsqlCommand("SELECT id FROM categories WHERE name = @name LIMIT 1", connection))
                        {
                            checkCmd.Parameters.AddWithValue("@name", transaction.Category);
                            var result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                categoryId = Convert.ToInt32(result);
                            }
                            else
                            {
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

                        DateTime dt;
                        if (!DateTime.TryParseExact(transaction.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        {
                            dt = DateTime.Now;
                        }

                        string rawAmount = transaction.Amount.Trim();
                        decimal amountValue = 0;
                        decimal.TryParse(rawAmount, NumberStyles.Number, new CultureInfo("ru-RU"), out amountValue);
                        string typeValue = transaction.Type;

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
                            if (count > 0)
                            {
                                current++;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ProgressBar.Value = (double)current / total * 100;
                                });
                                continue;
                            }
                        }

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

                        current++;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = (double)current / total * 100;
                        });
                    }
                }
            });

            ProgressText.Text = "Сохранение завершено!";
            await Task.Delay(500);
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Text = "";

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

            this.Close();
        }

        private async void ExportToOfx_Click(object sender, RoutedEventArgs e)
        {
            if (!(TransactionsGrid.ItemsSource is ObservableCollection<TransactionRecord> transactions && transactions.Count != 0))
            {
                MessageBox.Show("Нет данных для экспорта.");
                return;
            }

            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressText.Text = "Преобразование в OFX...";

            await Task.Run(() =>
            {
                OfxExporter.ExportToFile(transactions, (current, total) =>
                {
                    double progress = (double)current / total * 100;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = progress;
                    });
                });
            });

            ProgressText.Text = "Файл OFX успешно создан!";
            await Task.Delay(800);
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Text = "";
        }


        private void RadioButton_Checked_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "OFX Files|*.ofx",
                Title = "Выберите ofx выписку"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                txtFileName.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
            }
        }

        private void RadioButton_Checked_2(object sender, RoutedEventArgs e)
        {
            TransactionsGrid.IsReadOnly = false;
            TransactionsGrid.CanUserAddRows = true;

            // Очистить предыдущие данные и подготовить к ручному вводу
            TransactionsGrid.ItemsSource = new ObservableCollection<TransactionRecord>();
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