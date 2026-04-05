using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public sealed class DbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public bool VecLoaded { get; }

    public DbContext(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        };

        _connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        _connection.Open();
        VecLoaded = VecExtension.TryLoad(_connection);
        DbBootstrap.EnsureSchema(_connection, VecLoaded);
    }

    public SqliteConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();
}
