using Microsoft.Data.Sqlite;

namespace DataSetService;

public class UserService
{
    private readonly string _connectionString;

    public UserService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL UNIQUE COLLATE NOCASE,
                PasswordHash TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public bool ValidateUser(string email, string password)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        var hash = cmd.ExecuteScalar() as string;
        if (hash is null) return false;
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public void CreateUser(string email, string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Users (Email, PasswordHash) VALUES (@email, @hash)";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.ExecuteNonQuery();
    }

    public bool UserExists(string email)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
