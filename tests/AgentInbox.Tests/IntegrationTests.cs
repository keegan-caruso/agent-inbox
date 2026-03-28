using AgentInbox;
using AgentInbox.Database;
using Microsoft.Data.Sqlite;
using TUnit.Core;

namespace AgentInbox.Tests;

[NotInParallel]
public sealed class IntegrationTests : IDisposable
{
    private readonly string _dbPath;

    public IntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"agent-inbox-test-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        DeleteIfExists(_dbPath);
        DeleteIfExists($"{_dbPath}-wal");
        DeleteIfExists($"{_dbPath}-shm");
    }

    private DbContext CreateContext() => new DbContext(_dbPath);

    [Test]
    public async Task Register_NewAgent_InsertsRecord()
    {
        var result = await InvokeAsync("register", "alice", "--display-name", "Alice");

        await Assert.That(result.ExitCode).IsEqualTo(0);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE id='alice' AND deregistered_at IS NULL");
        await Assert.That(count).IsEqualTo(1L);
    }

    [Test]
    public async Task Deregister_SetsDeregisteredAt()
    {
        await InvokeAsync("register", "bob");
        var result = await InvokeAsync("deregister", "bob");

        await Assert.That(result.ExitCode).IsEqualTo(0);

        using var ctx = CreateContext();
        var conn = ctx.Connection;

        var deregisteredAt = Scalar<string>(conn, "SELECT deregistered_at FROM agents WHERE id='bob'");
        await Assert.That(deregisteredAt).IsNotNull();
    }

    [Test]
    public async Task Register_ReactivatedAgentWithoutDisplayName_PreservesExistingDisplayName()
    {
        await InvokeAsync("register", "bob", "--display-name", "Bob");
        await InvokeAsync("deregister", "bob");

        var result = await InvokeAsync("register", "bob");

        await Assert.That(result.ExitCode).IsEqualTo(0);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var displayName = Scalar<string>(conn, "SELECT display_name FROM agents WHERE id='bob' AND deregistered_at IS NULL");
        await Assert.That(displayName).IsEqualTo("Bob");
    }

    [Test]
    public async Task SendMessage_CreatesMessageAndRecipients()
    {
        await InvokeAsync("register", "alice");
        await InvokeAsync("register", "bob");
        var result = await InvokeAsync("send", "--from", "alice", "--to", "bob,bob", "--subject", "Hello", "--body", "Hi Bob!");

        await Assert.That(result.ExitCode).IsEqualTo(0);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var msgId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var count = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        var totalRecipients = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={msgId}");
        await Assert.That(count).IsEqualTo(1L);
        await Assert.That(totalRecipients).IsEqualTo(1L);
    }

    [Test]
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

        await Assert.That(ok.ExitCode).IsEqualTo(0);
        await Assert.That(denied.ExitCode).IsEqualTo(1);

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var isRead = Scalar<long>(verifyConn, $"SELECT is_read FROM message_recipients WHERE message_id={msgId} AND recipient_id='bob'");
        await Assert.That(isRead).IsEqualTo(1L);
    }

    [Test]
    public async Task Inbox_RequiresActiveAgent()
    {
        await InvokeAsync("register", "active-agent");
        await InvokeAsync("register", "deleted-agent");
        await InvokeAsync("deregister", "deleted-agent");

        var result = await InvokeAsync("inbox", "deleted-agent");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("active registered agent");
    }

    [Test]
    public async Task Agents_OnlyReturnsActiveAgents()
    {
        await InvokeAsync("register", "active-agent");
        await InvokeAsync("register", "deleted-agent");
        await InvokeAsync("deregister", "deleted-agent");

        using var ctx = CreateContext();
        var conn = ctx.Connection;

        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE deregistered_at IS NULL");
        await Assert.That(count).IsEqualTo(1L);
    }

    [Test]
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
        await Assert.That(denied.ExitCode).IsEqualTo(1);

        await InvokeAsync("deregister", "carol");
        var ok = await InvokeAsync("reply", "--from", "bob", "--to-message", originalId.ToString(), "--body", "reply");

        await Assert.That(ok.ExitCode).IsEqualTo(0);

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var replyId = Scalar<long>(verifyConn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var replyToId = Scalar<long>(verifyConn, $"SELECT reply_to_id FROM messages WHERE id={replyId}");
        await Assert.That(replyToId).IsEqualTo(originalId);

        var aliceRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={replyId} AND recipient_id='alice'");
        var carolRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id={replyId} AND recipient_id='carol'");
        await Assert.That(aliceRecipientCount).IsEqualTo(1L);
        await Assert.That(carolRecipientCount).IsEqualTo(0L);
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
            // Temporarily redirect Console so we can capture the CLI's output for assertions.
            // The original streams are always restored in the finally block.
#pragma warning disable TUnit0055
            Console.SetOut(stdout);
            Console.SetError(stderr);
#pragma warning restore TUnit0055

            var exitCode = await AgentInboxCli.InvokeAsync(allArgs);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
#pragma warning disable TUnit0055
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
#pragma warning restore TUnit0055
        }
    }

    private static T Scalar<T>(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
