using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public static class DbBootstrap
{
    /// <summary>
    /// Current schema version for migration tracking.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private const string FreshDatabaseRequiredMessage =
        "This version requires a fresh database. Migration/backward compatibility is not handled yet.";

    public static int EnsureSchema(SqliteConnection connection, bool vecLoaded = false)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

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

            CREATE TABLE IF NOT EXISTS groups (
                id          TEXT PRIMARY KEY,
                created_at  TEXT NOT NULL DEFAULT (datetime('now')),
                deleted_at  TEXT
            );

            CREATE TABLE IF NOT EXISTS group_members (
                group_id TEXT NOT NULL REFERENCES groups(id),
                agent_id TEXT NOT NULL REFERENCES agents(id),
                PRIMARY KEY (group_id, agent_id)
            );
            """;
        cmd.ExecuteNonQuery();

        ValidateAgentSchema(connection);

        using var indexCmd = connection.CreateCommand();
        indexCmd.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_agents_capability_token_hash
                ON agents (capability_token_hash);
            CREATE INDEX IF NOT EXISTS idx_group_members_agent_id
                ON group_members (agent_id);
            """;
        indexCmd.ExecuteNonQuery();

        // FTS5 full-text search (built into SQLite — always available)
        using var ftsCmd = connection.CreateCommand();
        ftsCmd.CommandText = """
            CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(
                subject,
                body,
                content='messages',
                content_rowid='id'
            );

            CREATE TRIGGER IF NOT EXISTS messages_fts_insert AFTER INSERT ON messages BEGIN
                INSERT INTO messages_fts(rowid, subject, body) VALUES (new.id, new.subject, new.body);
            END;

            CREATE TRIGGER IF NOT EXISTS messages_fts_delete BEFORE DELETE ON messages BEGIN
                INSERT INTO messages_fts(messages_fts, rowid, subject, body) VALUES ('delete', old.id, old.subject, old.body);
            END;

            CREATE TRIGGER IF NOT EXISTS messages_fts_update AFTER UPDATE ON messages BEGIN
                INSERT INTO messages_fts(messages_fts, rowid, subject, body) VALUES ('delete', old.id, old.subject, old.body);
                INSERT INTO messages_fts(rowid, subject, body) VALUES (new.id, new.subject, new.body);
            END;
            """;
        ftsCmd.ExecuteNonQuery();

        // Vector embeddings table (requires sqlite-vec extension)
        if (vecLoaded)
        {
            using var vecCmd = connection.CreateCommand();
            vecCmd.CommandText = """
                CREATE VIRTUAL TABLE IF NOT EXISTS message_embeddings USING vec0(
                    message_id INTEGER PRIMARY KEY,
                    embedding FLOAT[384]
                );
                """;
            vecCmd.ExecuteNonQuery();
        }

        // Track schema version
        using var versionCmd = connection.CreateCommand();
        versionCmd.CommandText = "INSERT OR IGNORE INTO schema_version (version) VALUES (@version)";
        versionCmd.Parameters.AddWithValue("@version", CurrentSchemaVersion);
        versionCmd.ExecuteNonQuery();

        return CurrentSchemaVersion;
    }

    public static int GetSchemaVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM schema_version ORDER BY version DESC LIMIT 1";
        var result = cmd.ExecuteScalar();
        return result is long version ? (int)version : 0;
    }

    private static void ValidateAgentSchema(SqliteConnection connection)
    {
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(agents)";

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var reader = pragmaCmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));

        if (!columns.Contains("capability_token_hash") || !columns.Contains("capability_token_created_at"))
            throw new InvalidOperationException(FreshDatabaseRequiredMessage);
    }
}
