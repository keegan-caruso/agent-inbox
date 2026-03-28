using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public static class DbBootstrap
{
    public const int CurrentSchemaVersion = 2;

    public static void EnsureSchema(SqliteConnection connection)
    {
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragmaCmd.ExecuteNonQuery();

        var userVersion = GetUserVersion(connection);
        var hasUserTables = HasUserTables(connection);

        if (!hasUserTables && userVersion == 0)
        {
            CreateCurrentSchema(connection);
            SetUserVersion(connection, CurrentSchemaVersion);
        }
        else if (userVersion == CurrentSchemaVersion)
        {
            ValidateCurrentSchema(connection);
        }
        else if (hasUserTables && userVersion == 0)
        {
            throw new InvalidOperationException(
                "This database uses an unversioned legacy schema. Migration is not implemented yet.");
        }
        else if (userVersion < CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {userVersion} is older than supported version {CurrentSchemaVersion}. Migration is not implemented yet.");
        }
        else
        {
            throw new InvalidOperationException(
                $"Database schema version {userVersion} is newer than this application supports ({CurrentSchemaVersion}).");
        }
    }

    private static void CreateCurrentSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS agents (
                id              TEXT PRIMARY KEY,
                display_name    TEXT,
                capability_token_hash TEXT NOT NULL,
                capability_token_created_at TEXT NOT NULL DEFAULT (datetime('now')),
                registered_at   TEXT NOT NULL DEFAULT (datetime('now')),
                deregistered_at TEXT
            );

            CREATE TABLE IF NOT EXISTS messages (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                sender_id    TEXT NOT NULL REFERENCES agents(id),
                subject      TEXT,
                body         TEXT NOT NULL,
                reply_to_id  INTEGER REFERENCES messages(id),
                created_at   TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS message_recipients (
                message_id   INTEGER NOT NULL REFERENCES messages(id),
                recipient_id TEXT    NOT NULL REFERENCES agents(id),
                is_read      INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (message_id, recipient_id)
            );
            """;
        cmd.ExecuteNonQuery();

        using var indexCmd = connection.CreateCommand();
        indexCmd.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_agents_capability_token_hash
                ON agents (capability_token_hash);
            """;
        indexCmd.ExecuteNonQuery();
    }

    private static void ValidateCurrentSchema(SqliteConnection connection)
    {
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(agents)";

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = pragmaCmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));

        if (!columns.Contains("capability_token_hash") || !columns.Contains("capability_token_created_at"))
            throw new InvalidOperationException(
                "Database schema is corrupted or invalid for the current version.");
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version}";
        cmd.ExecuteNonQuery();
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        return (long)cmd.ExecuteScalar()! > 0;
    }
}
