using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public static class DbBootstrap
{
    private const string FreshDatabaseRequiredMessage =
        "This version requires a fresh database. Migration/backward compatibility is not handled yet.";

    public static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

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

            CREATE TABLE IF NOT EXISTS message_groups (
                message_id INTEGER NOT NULL REFERENCES messages(id),
                group_id TEXT NOT NULL REFERENCES groups(id),
                PRIMARY KEY (message_id, group_id)
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
            CREATE INDEX IF NOT EXISTS idx_message_groups_group_id
                ON message_groups (group_id);
            """;
        indexCmd.ExecuteNonQuery();

        // Load vec extension and create vector tables
        VecExtension.Load(connection);
        EnsureVectorSchema(connection);
    }

    private static void EnsureVectorSchema(SqliteConnection connection)
    {
        try
        {
            // Create vector table for message embeddings
            // Using 32-dimensional embeddings for message content
            using var vecCmd = connection.CreateCommand();
            vecCmd.CommandText = """
                CREATE VIRTUAL TABLE IF NOT EXISTS message_embeddings USING vec0(
                    message_id INTEGER PRIMARY KEY,
                    embedding FLOAT[32]
                );
                """;
            vecCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // If vec extension is not available, skip vector schema creation
            // This allows the app to work without semantic search
        }
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
