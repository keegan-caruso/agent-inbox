using AgentInbox;
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
    public async Task Register_NewAgent_InsertsRecord()
    {
        var result = await InvokeAsync("register", "alice", "--display-name", "Alice");

        Assert.Equal(0, result.ExitCode);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE id='alice' AND deregistered_at IS NULL");
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Deregister_SetsDeregisteredAt()
    {
        await InvokeAsync("register", "bob");
        var result = await InvokeAsync("deregister", "bob");

        Assert.Equal(0, result.ExitCode);

        using var ctx = CreateContext();
        var conn = ctx.Connection;

        var deregisteredAt = Scalar<string>(conn, "SELECT deregistered_at FROM agents WHERE id='bob'");
        Assert.NotNull(deregisteredAt);
    }

    [Fact]
    public async Task SendMessage_CreatesMessageAndRecipients()
    {
        await InvokeAsync("register", "alice");
        await InvokeAsync("register", "bob");
        var result = await InvokeAsync("send", "--from", "alice", "--to", "bob,bob", "--subject", "Hello", "--body", "Hi Bob!");

        Assert.Equal(0, result.ExitCode);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var msgId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var count = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        var totalRecipients = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={msgId}");
        Assert.Equal(1L, count);
        Assert.Equal(1L, totalRecipients);
    }

    [Fact]
    public async Task ReadMessage_MarksAsRead_AndRejectsOtherAgents()
    {
        await InvokeAsync("register", "alice");
        await InvokeAsync("register", "bob");
        await InvokeAsync("register", "carol");
        await InvokeAsync("send", "--from", "alice", "--to", "bob", "--body", "test");

        long msgId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            msgId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        }

        var ok = await InvokeAsync("read", msgId.ToString(), "--as", "bob");
        var denied = await InvokeAsync("read", msgId.ToString(), "--as", "carol");

        Assert.Equal(0, ok.ExitCode);
        Assert.Equal(1, denied.ExitCode);

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var isRead = Scalar<long>(verifyConn, $"SELECT is_read FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        Assert.Equal(1L, isRead);
    }

    [Fact]
    public async Task Inbox_RequiresActiveAgent()
    {
        await InvokeAsync("register", "active-agent");
        await InvokeAsync("register", "deleted-agent");
        await InvokeAsync("deregister", "deleted-agent");

        var result = await InvokeAsync("inbox", "deleted-agent");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("active registered agent", result.StdErr);
    }

    [Fact]
    public async Task Agents_OnlyReturnsActiveAgents()
    {
        await InvokeAsync("register", "active-agent");
        await InvokeAsync("register", "deleted-agent");
        await InvokeAsync("deregister", "deleted-agent");

        using var ctx = CreateContext();
        var conn = ctx.Connection;

        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE deregistered_at IS NULL");
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task ReplyMessage_RequiresParticipant_AndSkipsInactiveRecipients()
    {
        await InvokeAsync("register", "alice");
        await InvokeAsync("register", "bob");
        await InvokeAsync("register", "carol");
        await InvokeAsync("register", "mallory");
        await InvokeAsync("send", "--from", "alice", "--to", "bob,carol", "--body", "original");

        long originalId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            originalId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        }

        var denied = await InvokeAsync("reply", "--from", "mallory", "--to-message", originalId.ToString(), "--body", "reply");
        Assert.Equal(1, denied.ExitCode);

        await InvokeAsync("deregister", "carol");
        var ok = await InvokeAsync("reply", "--from", "bob", "--to-message", originalId.ToString(), "--body", "reply");

        Assert.Equal(0, ok.ExitCode);

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var replyId = Scalar<long>(verifyConn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var replyToId = Scalar<long>(verifyConn, $"SELECT reply_to_id FROM messages WHERE id={replyId}");
        Assert.Equal(originalId, replyToId);

        var aliceRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={replyId} AND recipient_id='alice'");
        var carolRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={replyId} AND recipient_id='carol'");
        Assert.Equal(1L, aliceRecipientCount);
        Assert.Equal(0L, carolRecipientCount);
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(params string[] args)
    {
        var allArgs = args.Concat(["--db-path", _dbPath]).ToArray();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await AgentInboxCli.InvokeAsync(allArgs);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static T Scalar<T>(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }
}
