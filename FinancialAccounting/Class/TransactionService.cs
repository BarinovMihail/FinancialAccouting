using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace FinancialAccounting.Class
{
    public class TransactionService
    {
        private readonly DatabaseManager _db;
        private readonly String _username;
        private readonly int _accountId;
        public TransactionService(DatabaseManager db, string username, int accountId)
        {
            _username = username;
            _db = db;
            _accountId = accountId;
        }

        public string SaveTransactions(IEnumerable<TransactionRecord> transactions, string username, int accountId)
        {
            if (transactions == null || !transactions.Any())
                return "Нет данных для сохранения.";

            var connection = _db.GetOpenConnection();

            // Получаем userId
            int userId;
            using (var userIdCmd = new NpgsqlCommand("SELECT get_user_id(@username)", connection))
            {
                userIdCmd.Parameters.AddWithValue("@username", _username);
                userId = Convert.ToInt32(userIdCmd.ExecuteScalar());
            }

            foreach (var transaction in transactions)
            {
                int categoryId;
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT id FROM categories WHERE name = @name LIMIT 1",
                    connection))
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
                            "INSERT INTO categories (name) VALUES (@name) RETURNING id",
                            connection))
                        {
                            insertCategoryCmd.Parameters.AddWithValue("@name", transaction.Category);
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

            return "OK";
        }
    }
}
