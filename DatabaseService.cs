using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace PersonalExpenseTracker
{
    public class DatabaseService
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>%LocalAppData%\PersonalExpenseTracker\expenses.db — independent of the working directory.</summary>
        public static string DbPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PersonalExpenseTracker", "expenses.db");

        private readonly string _connectionString = $"Data Source={DbPath}";

        public DatabaseService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            MigrateLegacyDatabase();
            InitializeDatabase();
        }

        /// <summary>
        /// v1.0 stored expenses.db in the current working directory. Move it to the new
        /// location once, so existing data is not lost.
        /// </summary>
        private static void MigrateLegacyDatabase()
        {
            if (File.Exists(DbPath)) return;
            foreach (var dir in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                var legacy = Path.Combine(dir, "expenses.db");
                if (File.Exists(legacy))
                {
                    File.Copy(legacy, DbPath);
                    return;
                }
            }
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Amount DECIMAL NOT NULL,
                    Description TEXT
                );";
            command.ExecuteNonQuery();
        }

        public void AddTransaction(Transaction transaction)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Transactions (Date, Type, Category, Amount, Description)
                VALUES ($date, $type, $category, $amount, $description)";

            // Always Gregorian/invariant: on a Thai-locale PC the default culture writes Buddhist-era years.
            command.Parameters.AddWithValue("$date", transaction.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$type", transaction.Type);
            command.Parameters.AddWithValue("$category", transaction.Category);
            command.Parameters.AddWithValue("$amount", transaction.Amount);
            command.Parameters.AddWithValue("$description", transaction.Description ?? string.Empty);
            command.ExecuteNonQuery();
        }

        public void DeleteTransaction(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Transactions WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public List<Transaction> GetAllTransactions()
        {
            var transactions = new List<Transaction>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Date, Type, Category, Amount, Description FROM Transactions ORDER BY Date DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                transactions.Add(new Transaction
                {
                    Id = reader.GetInt32(0),
                    Date = ParseDate(reader.GetString(1)),
                    Type = reader.GetString(2),
                    Category = reader.GetString(3),
                    Amount = reader.GetDecimal(4),
                    Description = reader.IsDBNull(5) ? "" : reader.GetString(5)
                });
            }
            return transactions;
        }

        /// <summary>Reads invariant dates, and repairs Buddhist-era years written by v1.0 on Thai-locale PCs.</summary>
        private static DateTime ParseDate(string text)
        {
            if (!DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return DateTime.MinValue;
            }
            return date.Year > 2400 ? date.AddYears(-543) : date;
        }

        /// <summary>Writes transactions as UTF-8 CSV with BOM so Excel shows Thai correctly.</summary>
        public static void ExportCsv(string path, IEnumerable<Transaction> transactions)
        {
            static string Esc(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine("วันที่,ประเภท,หมวดหมู่,จำนวนเงิน,รายละเอียด");
            foreach (var t in transactions)
            {
                sb.Append(t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(Esc(t.Type)).Append(',')
                  .Append(Esc(t.Category)).Append(',')
                  .Append(t.Amount.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .AppendLine(Esc(t.Description));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
