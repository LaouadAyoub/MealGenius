using Npgsql;

namespace MealGeniusBackend.Services;

// Session advisory locks serialize retries across app instances without holding a long database transaction.
public sealed class DatabaseWorkLock : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly string key;
    private DatabaseWorkLock(NpgsqlConnection connection, string key) { this.connection = connection; this.key = key; }
    public static async Task<DatabaseWorkLock> Acquire(string connectionString, string key, CancellationToken token)
    {
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        try
        {
            await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", connection);
            command.Parameters.AddWithValue("key", key);
            command.CommandTimeout = 0;
            await command.ExecuteNonQueryAsync(token);
            return new DatabaseWorkLock(connection, key);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@key, 0))", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync();
        }
        finally { await connection.DisposeAsync(); }
    }
}
