using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using LiveCharts.Wpf;
using LiveCharts;
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
using Npgsql;
using System.Net.Http;
using FinancialAccounting.Class;

namespace FinancialAccounting
{
    /// <summary>
    /// Логика взаимодействия для AnalyticsWindow.xaml
    /// </summary>
    public partial class AnalyticsWindow : Window
    {
        private int _accountId;
        public AnalyticsWindow(int accountId)
        {
            InitializeComponent();
            _accountId = accountId;
            LoadCategories();
        }
        private List<TransactionPoint> data = new List<TransactionPoint>();
        private void LoadCategories()
        {
            CategoryComboBox.Items.Clear();
            CategoryComboBox.Items.Add(new ComboBoxItem { Content = "Все", Tag = null });

            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();
                var command = new NpgsqlCommand("SELECT id, name FROM categories", connection);
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    CategoryComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = reader["name"].ToString(),
                        Tag = reader.GetInt32(0)
                    });
                }
                reader.Close();
            }
            CategoryComboBox.SelectedIndex = 0;
        }


        private void BuildChart_Click(object sender, RoutedEventArgs e)
        {
            string chartType = (ChartTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string operationType = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            var selectedCategory = CategoryComboBox.SelectedItem as ComboBoxItem;
            DateTime? startDate = StartDatePicker.SelectedDate;
            DateTime? endDate = EndDatePicker.SelectedDate;

            string query = "SELECT date, amount, description FROM transactions WHERE accountid = @accountid";
            if (operationType != null && operationType != "Все")
                query += " AND type = @type";
            if (selectedCategory != null && selectedCategory.Tag != null)
                query += " AND categoryid = @categoryid";
            if (startDate.HasValue)
                query += " AND date >= @startDate";
            if (endDate.HasValue)
                query += " AND date <= @endDate";

            data.Clear();
            using (var dbManager = new DatabaseManager())
            {
                var connection = dbManager.GetOpenConnection();
                var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@accountid", _accountId);
                if (operationType != null && operationType != "Все")
                    command.Parameters.AddWithValue("@type", operationType == "Доход" ? "Income" : "Expense");
                if (selectedCategory != null && selectedCategory.Tag != null)
                    command.Parameters.AddWithValue("@categoryid", (int)selectedCategory.Tag);
                if (startDate.HasValue)
                    command.Parameters.AddWithValue("@startDate", startDate.Value.Date);
                if (endDate.HasValue)
                    command.Parameters.AddWithValue("@endDate", endDate.Value.Date.AddDays(1).AddTicks(-1));

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new TransactionPoint
                    {
                        Date = reader.GetDateTime(0),
                        Amount = reader.GetDecimal(1),
                        Description = reader.GetString(2)
                    });
                }
                reader.Close();
            }

            MainChart.Series.Clear();
            MainChart.AxisX.Clear();
            MainChart.AxisY.Clear();
            PieChart.Series.Clear();

            if (chartType == "Гистограмма")
            {
                MainChart.Visibility = Visibility.Visible;
                PieChart.Visibility = Visibility.Collapsed;

                var columnSeries = new ColumnSeries
                {
                    Title = "Сумма",
                    Values = new ChartValues<decimal>(data.Select(d => d.Amount)),
                    DataLabels = false,
                    LabelPoint = point =>
                    {
                        var tp = data[(int)point.X];
                        return $"Дата: {tp.Date.ToShortDateString()}\nСумма: {tp.Amount}\nОписание: {tp.Description}";
                    }
                };
                MainChart.Series.Add(columnSeries);
                MainChart.AxisX.Add(new Axis
                {
                    Title = "Дата",
                    Labels = data.Select(d => d.Date.ToShortDateString()).ToArray()
                });
                MainChart.AxisY.Add(new Axis { Title = "Сумма" });
            }
            else if (chartType == "Линейный график")
            {
                MainChart.Visibility = Visibility.Visible;
                PieChart.Visibility = Visibility.Collapsed;

                var lineSeries = new LineSeries
                {
                    Title = "Сумма",
                    Values = new ChartValues<decimal>(data.Select(d => d.Amount)),
                    DataLabels = false,
                    LabelPoint = point =>
                    {
                        var tp = data[(int)point.X];
                        return $"Дата: {tp.Date.ToShortDateString()}\nСумма: {tp.Amount}\nОписание: {tp.Description}";
                    }
                };
                MainChart.Series.Add(lineSeries);
                MainChart.AxisX.Add(new Axis
                {
                    Title = "Дата",
                    Labels = data.Select(d => d.Date.ToShortDateString()).ToArray()
                });
                MainChart.AxisY.Add(new Axis { Title = "Сумма" });
            }
            else if (chartType == "Круговая диаграмма")
            {
                MainChart.Visibility = Visibility.Collapsed;
                PieChart.Visibility = Visibility.Visible;

                foreach (var group in data.GroupBy(d => d.Description))
                {
                    PieChart.Series.Add(new PieSeries
                    {
                        Title = group.Key,
                        Values = new ChartValues<decimal> { group.Sum(g => g.Amount) },
                        DataLabels = false,
                        LabelPoint = chartPoint =>
                            $"Описание: {group.Key}\nСумма: {chartPoint.Y}"
                    });
                }
            }
        }
       
        private async void GenerateAnalysis_Click(object sender, RoutedEventArgs e)
        {
            NeuroAnalysisText.Text = "Анализируем данные...";

            var summary = new StringBuilder();
            summary.AppendLine($"Период: {StartDatePicker.SelectedDate?.ToShortDateString()} - {EndDatePicker.SelectedDate?.ToShortDateString()}");
            summary.AppendLine($"Тип операций: {(TypeComboBox.SelectedItem as ComboBoxItem)?.Content ?? "Все"}");
            summary.AppendLine($"Категория: {(CategoryComboBox.SelectedItem as ComboBoxItem)?.Content ?? "Все"}");
            summary.AppendLine($"Количество транзакций: {data.Count}");
            summary.AppendLine($"Общая сумма: {data.Sum(d => d.Amount):C}");

            if (data.Count > 0)
            {
                summary.AppendLine("\nПримеры транзакций:");
                foreach (var transaction in data.Take(10))
                {
                    summary.AppendLine($"- {transaction.Date:d}: {transaction.Amount:C} ({transaction.Description})");
                }
            }

            try
            {
                var mistralService = new MistralService(new HttpClient());
                string analysis = await mistralService.GetAnalysisAsync(summary.ToString());
                NeuroAnalysisText.Text = analysis;
            }
            catch (Exception ex)
            {
                NeuroAnalysisText.Text = $"Ошибка анализа: {ex.Message}";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
   
    public class TransactionPoint
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}

