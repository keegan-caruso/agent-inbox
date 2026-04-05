using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public sealed class DbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public bool VecLoaded { get; }
    public int SchemaVersion { get; }

    public DbContext(string dbPath, string dbPathSource = "default")
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
        SchemaVersion = DbBootstrap.EnsureSchema(_connection, VecLoaded);

        // Emit diagnostic event about database opening
        Diagnostics.DiagnosticManager.EmitDatabaseOpened(dbPath, dbPathSource, SchemaVersion, VecLoaded);
    }

    public SqliteConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();
}
