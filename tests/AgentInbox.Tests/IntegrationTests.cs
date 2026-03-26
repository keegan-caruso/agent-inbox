using AgentInbox.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AgentInbox.Tests;

public sealed class IntegrationTests : IDisposable
{
    private readonly string _dbPath;

    public IntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"agent-inbox-test-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private DbContext CreateContext() => new DbContext(_dbPath);

    [Fact]
    public void Register_NewAgent_InsertsRecord()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id, display_name) VALUES ('alice', 'Alice')");

        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE id='alice' AND deregistered_at IS NULL");
        Assert.Equal(1L, count);
    }

    [Fact]
    public void Deregister_SetsDeregisteredAt()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id) VALUES ('bob')");
        Insert(conn, "UPDATE agents SET deregistered_at = datetime('now') WHERE id='bob'");

        var deregisteredAt = Scalar<string>(conn, "SELECT deregistered_at FROM agents WHERE id='bob'");
        Assert.NotNull(deregisteredAt);
    }

    [Fact]
    public void SendMessage_CreatesMessageAndRecipients()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id) VALUES ('alice')");
        Insert(conn, "INSERT INTO agents (id) VALUES ('bob')");

        Insert(conn, "INSERT INTO messages (sender_id, subject, body) VALUES ('alice', 'Hello', 'Hi Bob!')");
        var msgId = Scalar<long>(conn, "SELECT last_insert_rowid()");
        Insert(conn, $"INSERT INTO message_recipients (message_id, recipient_id) VALUES ({msgId}, 'bob')");

        var count = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        Assert.Equal(1L, count);
    }

    [Fact]
    public void ReadMessage_MarksAsRead()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id) VALUES ('alice')");
        Insert(conn, "INSERT INTO agents (id) VALUES ('bob')");
        Insert(conn, "INSERT INTO messages (sender_id, body) VALUES ('alice', 'test')");
        var msgId = Scalar<long>(conn, "SELECT last_insert_rowid()");
        Insert(conn, $"INSERT INTO message_recipients (message_id, recipient_id, is_read) VALUES ({msgId}, 'bob', 0)");

        Insert(conn, $"UPDATE message_recipients SET is_read = 1 WHERE message_id={msgId} AND recipient_id='bob'");

        var isRead = Scalar<long>(conn, $"SELECT is_read FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        Assert.Equal(1L, isRead);
    }

    [Fact]
    public void Agents_OnlyReturnsActiveAgents()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id) VALUES ('active-agent')");
        Insert(conn, "INSERT INTO agents (id) VALUES ('deleted-agent')");
        Insert(conn, "UPDATE agents SET deregistered_at = datetime('now') WHERE id='deleted-agent'");

        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE deregistered_at IS NULL");
        Assert.Equal(1L, count);
    }

    [Fact]
    public void ReplyMessage_CreatesReplyWithReplyToId()
    {
        using var ctx = CreateContext();
        var conn = ctx.Connection;

        Insert(conn, "INSERT INTO agents (id) VALUES ('alice')");
        Insert(conn, "INSERT INTO agents (id) VALUES ('bob')");
        Insert(conn, "INSERT INTO messages (sender_id, body) VALUES ('alice', 'original')");
        var originalId = Scalar<long>(conn, "SELECT last_insert_rowid()");
        Insert(conn, $"INSERT INTO message_recipients (message_id, recipient_id) VALUES ({originalId}, 'bob')");

        Insert(conn, $"INSERT INTO messages (sender_id, body, reply_to_id) VALUES ('bob', 'reply', {originalId})");
        var replyId = Scalar<long>(conn, "SELECT last_insert_rowid()");
        Insert(conn, $"INSERT INTO message_recipients (message_id, recipient_id) VALUES ({replyId}, 'alice')");

        var replyToId = Scalar<long>(conn, $"SELECT reply_to_id FROM messages WHERE id={replyId}");
        Assert.Equal(originalId, replyToId);
    }

    private static void Insert(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }
}
