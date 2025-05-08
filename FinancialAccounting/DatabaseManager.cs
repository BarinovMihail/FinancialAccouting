using System;
using Npgsql;

namespace FinancialAccounting
{
    public class DatabaseManager : IDisposable
    {
        private readonly string connectionString;
        private NpgsqlConnection connection;

        public DatabaseManager()
        {
            // Формируем строку подключения
            connectionString = "Host=localhost;Database=FinancialAccounting;Username=postgres;Password=123";
        }

        /// <summary>
        /// Возвращает открытое соединение.
        /// Если соединение ещё не создано, оно создаётся и открывается.
        /// </summary>
        public NpgsqlConnection GetOpenConnection()
        {
            if (connection == null)
            {
                connection = new NpgsqlConnection(connectionString);
            }
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }
            return connection;
        }

        /// <summary>
        /// Закрывает соединение, если оно открыто.
        /// </summary>
        public void CloseConnection()
        {
            if (connection != null && connection.State != System.Data.ConnectionState.Closed)
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Освобождает ресурсы, связанные с соединением.
        /// </summary>
        public void Dispose()
        {
            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }
        }
    }
}
