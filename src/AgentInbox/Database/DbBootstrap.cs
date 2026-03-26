using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public static class DbBootstrap
{
    public static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS agents (
                id              TEXT PRIMARY KEY,
                display_name    TEXT,
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
    }
}
