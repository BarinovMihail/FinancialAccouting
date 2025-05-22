using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialAccounting
{
    public static class UserService
    {
        public static string RegisterUser(string username, string email, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return "Пожалуйста, заполните все поля.";

            string hashedPassword = PasswordEncryptor.Encrypt(password);

            using (var db = new DatabaseManager())
            using (var cmd = db.GetOpenConnection().CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM users WHERE login = @login OR email = @email";
                cmd.Parameters.AddWithValue("login", username);
                cmd.Parameters.AddWithValue("email", email);

                long userExists = (long)cmd.ExecuteScalar();
                if (userExists > 0)
                    return "Пользователь с таким логином или email уже существует.";

                cmd.CommandText = "INSERT INTO users (login, email, passwordhash) VALUES (@login, @email, @password)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("login", username);
                cmd.Parameters.AddWithValue("email", email);
                cmd.Parameters.AddWithValue("password", hashedPassword);
                cmd.ExecuteNonQuery();

                return "OK";
            }
        }
    }
}
