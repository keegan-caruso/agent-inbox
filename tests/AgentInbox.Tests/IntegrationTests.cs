using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentInbox;
using AgentInbox.Database;
using Microsoft.Data.Sqlite;
using TUnit.Core;

namespace AgentInbox.Tests;

[NotInParallel]
public sealed class IntegrationTests : IDisposable
{
    private const string CapabilityTokenEnvVar = "AGENT_INBOX_CAPABILITY_TOKEN";
    private readonly string _dbPath;

    public IntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"agent-inbox-test-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CapabilityTokenEnvVar, null);
        DeleteIfExists(_dbPath);
        DeleteIfExists($"{_dbPath}-wal");
        DeleteIfExists($"{_dbPath}-shm");
    }

    private DbContext CreateContext() => new DbContext(_dbPath);

    [Test]
    public async Task Register_ReturnsCapabilityToken_AndStoresOnlyTheHash()
    {
        var result = await InvokeAsync("register", "alice", "--display-name", "Alice", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(0);

        var registration = ParseRegistration(result.StdOut);
        var parsesAsGuid = Guid.TryParse(registration.CapabilityToken, out _);

        await Assert.That(registration.Message).IsEqualTo("Agent 'alice' registered successfully.");
        await Assert.That(registration.AgentId).IsEqualTo("alice");
        await Assert.That(parsesAsGuid).IsTrue();
        await Assert.That(IsVersion7Guid(registration.CapabilityToken)).IsTrue();

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE id='alice' AND deregistered_at IS NULL");
        var storedHash = Scalar<string>(conn, "SELECT capability_token_hash FROM agents WHERE id='alice'");
        var tokenCreatedAt = Scalar<string>(conn, "SELECT capability_token_created_at FROM agents WHERE id='alice'");

        await Assert.That(count).IsEqualTo(1L);
        await Assert.That(storedHash).IsEqualTo(HashCapabilityToken(registration.CapabilityToken));
        await Assert.That(storedHash == registration.CapabilityToken).IsFalse();
        await Assert.That(tokenCreatedAt).IsNotNull();
    }

    [Test]
    public async Task Register_PlainAndNdjsonOutputIncludeCapabilityToken_AndActiveAgentStillFails()
    {
        var plain = await InvokeAsync("register", "plain-agent");
        var ndjson = await InvokeAsync("register", "ndjson-agent", "--format", "ndjson");
        var duplicate = await InvokeAsync("register", "plain-agent", "--format", "json");

        await Assert.That(plain.ExitCode).IsEqualTo(0);
        await Assert.That(plain.StdOut).Contains("Capability Token:");

        await Assert.That(ndjson.ExitCode).IsEqualTo(0);
        await Assert.That(ParseRegistration(ndjson.StdOut).CapabilityToken.Length > 0).IsTrue();

        await Assert.That(duplicate.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(duplicate.StdErr)).IsEqualTo("Agent 'plain-agent' is already registered.");
    }

    [Test]
    public async Task Register_ReactivationPreservesDisplayName_RotatesToken_AndInvalidatesTheOldOne()
    {
        var alice = await RegisterAgentAsync("alice");
        var firstBob = await RegisterAgentAsync("bob", "Bob");

        await InvokeAsync("deregister", "bob");

        var afterDeregister = await InvokeAsync(
            "send",
            "--token", firstBob.CapabilityToken,
            "--to", "alice",
            "--body", "should fail",
            "--format", "json");

        await Assert.That(afterDeregister.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(afterDeregister.StdErr)).IsEqualTo("Invalid token.");

        var secondBob = await RegisterAgentAsync("bob");
        var oldTokenAfterReactivation = await InvokeAsync(
            "send",
            "--token", firstBob.CapabilityToken,
            "--to", "alice",
            "--body", "still invalid",
            "--format", "json");
        var newTokenAfterReactivation = await InvokeAsync(
            "send",
            "--token", secondBob.CapabilityToken,
            "--to", "alice",
            "--body", "works",
            "--format", "json");

        await Assert.That(secondBob.Message).IsEqualTo("Agent 'bob' reactivated.");
        await Assert.That(secondBob.CapabilityToken == firstBob.CapabilityToken).IsFalse();
        await Assert.That(oldTokenAfterReactivation.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(oldTokenAfterReactivation.StdErr)).IsEqualTo("Invalid token.");
        await Assert.That(newTokenAfterReactivation.ExitCode).IsEqualTo(0);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var displayName = Scalar<string>(conn, "SELECT display_name FROM agents WHERE id='bob' AND deregistered_at IS NULL");
        var storedHash = Scalar<string>(conn, "SELECT capability_token_hash FROM agents WHERE id='bob'");

        await Assert.That(displayName).IsEqualTo("Bob");
        await Assert.That(storedHash).IsEqualTo(HashCapabilityToken(secondBob.CapabilityToken));
        await Assert.That(alice.AgentId).IsEqualTo("alice");
    }

    [Test]
    public async Task Send_UsesCapabilityTokens_WithEnvVarFallback_AndCliOverride()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");

        var firstSend = await InvokeAsync(
            "send",
            "--token", alice.CapabilityToken,
            "--to", "bob,bob",
            "--subject", "Hello",
            "--body", "Hi Bob!");

        await Assert.That(firstSend.ExitCode).IsEqualTo(0);

        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            var firstMessageId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id ASC LIMIT 1");
            var senderId = Scalar<string>(conn, $"SELECT sender_id FROM messages WHERE id = {firstMessageId}");
            var totalRecipients = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {firstMessageId}");

            await Assert.That(senderId).IsEqualTo("alice");
            await Assert.That(totalRecipients).IsEqualTo(1L);
        }

        var originalEnvToken = Environment.GetEnvironmentVariable(CapabilityTokenEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(CapabilityTokenEnvVar, bob.CapabilityToken);
            var envVarSend = await InvokeAsync("send", "--to", "alice", "--body", "via env");
            await Assert.That(envVarSend.ExitCode).IsEqualTo(0);

            Environment.SetEnvironmentVariable(CapabilityTokenEnvVar, bob.CapabilityToken);
            var cliOverrideSend = await InvokeAsync("send", "--token", carol.CapabilityToken, "--to", "alice", "--body", "via cli");
            await Assert.That(cliOverrideSend.ExitCode).IsEqualTo(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CapabilityTokenEnvVar, originalEnvToken);
        }

        var selfSend = await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "alice", "--body", "self");
        var invalidToken = await InvokeAsync("send", "--token", Guid.NewGuid().ToString(), "--to", "alice", "--body", "bad", "--format", "json");
        var missingToken = await InvokeAsync("send", "--to", "alice", "--body", "missing", "--format", "json");

        await Assert.That(selfSend.ExitCode).IsEqualTo(0);
        await Assert.That(invalidToken.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(invalidToken.StdErr)).IsEqualTo("Invalid token.");
        await Assert.That(missingToken.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missingToken.StdErr)).IsEqualTo("Capability token is required.");

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var secondMessageSender = Scalar<string>(verifyConn, "SELECT sender_id FROM messages ORDER BY id ASC LIMIT 1 OFFSET 1");
        var thirdMessageSender = Scalar<string>(verifyConn, "SELECT sender_id FROM messages ORDER BY id ASC LIMIT 1 OFFSET 2");
        var selfMessageId = Scalar<long>(verifyConn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var selfRecipients = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {selfMessageId}");

        await Assert.That(secondMessageSender).IsEqualTo("bob");
        await Assert.That(thirdMessageSender).IsEqualTo("carol");
        await Assert.That(selfRecipients).IsEqualTo(1L);
    }

    [Test]
    public async Task Read_UsesCapabilityToken_AndMarksMessagesAsRead()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", "test");

        long messageId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            messageId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        }

        var ok = await InvokeAsync("read", messageId.ToString(), "--token", bob.CapabilityToken, "--format", "json");
        var denied = await InvokeAsync("read", messageId.ToString(), "--token", carol.CapabilityToken, "--format", "json");
        var invalid = await InvokeAsync("read", messageId.ToString(), "--token", Guid.NewGuid().ToString(), "--format", "json");
        var missing = await InvokeAsync("read", messageId.ToString(), "--format", "json");

        await Assert.That(ok.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJson(ok.StdOut).GetProperty("body").GetString()).IsEqualTo("test");
        await Assert.That(denied.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(denied.StdErr)).IsEqualTo($"Message {messageId} not found for agent 'carol'.");
        await Assert.That(invalid.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(invalid.StdErr)).IsEqualTo("Invalid token.");
        await Assert.That(missing.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missing.StdErr)).IsEqualTo("Capability token is required.");

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var isRead = Scalar<long>(verifyConn, $"SELECT is_read FROM message_recipients WHERE message_id = {messageId} AND recipient_id = 'bob'");
        await Assert.That(isRead).IsEqualTo(1L);
    }

    [Test]
    public async Task Inbox_UsesCapabilityToken_AndUnreadOnlyStillWorks()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", "first");
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", "second");
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "carol", "--body", "third");

        long bobFirstMessageId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            bobFirstMessageId = Scalar<long>(conn, """
                SELECT mr.message_id
                FROM message_recipients mr
                WHERE mr.recipient_id = 'bob'
                ORDER BY mr.message_id ASC
                LIMIT 1
                """);
        }

        await InvokeAsync("read", bobFirstMessageId.ToString(), "--token", bob.CapabilityToken);

        var inbox = await InvokeAsync("inbox", "--token", bob.CapabilityToken, "--format", "json");
        var unreadOnly = await InvokeAsync("inbox", "--token", bob.CapabilityToken, "--unread-only", "--format", "json");
        var invalid = await InvokeAsync("inbox", "--token", Guid.NewGuid().ToString(), "--format", "json");
        var missing = await InvokeAsync("inbox", "--format", "json");

        await Assert.That(inbox.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(inbox.StdOut).Length).IsEqualTo(2);
        await Assert.That(unreadOnly.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(unreadOnly.StdOut).Length).IsEqualTo(1);
        await Assert.That(invalid.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(invalid.StdErr)).IsEqualTo("Invalid token.");
        await Assert.That(missing.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missing.StdErr)).IsEqualTo("Capability token is required.");
    }

    [Test]
    public async Task Reply_UsesCapabilityToken_RequiresParticipants_AndSkipsInactiveRecipients()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");
        var mallory = await RegisterAgentAsync("mallory");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob,carol", "--body", "original");

        long originalId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            originalId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        }

        var denied = await InvokeAsync("reply", "--token", mallory.CapabilityToken, "--to-message", originalId.ToString(), "--body", "reply", "--format", "json");
        await Assert.That(denied.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(denied.StdErr)).IsEqualTo($"Sender 'mallory' is not a participant in message {originalId} and cannot reply to it.");

        await InvokeAsync("deregister", "carol");

        var ok = await InvokeAsync("reply", "--token", bob.CapabilityToken, "--to-message", originalId.ToString(), "--body", "reply");
        var invalid = await InvokeAsync("reply", "--token", Guid.NewGuid().ToString(), "--to-message", originalId.ToString(), "--body", "bad", "--format", "json");
        var missing = await InvokeAsync("reply", "--to-message", originalId.ToString(), "--body", "missing", "--format", "json");

        await Assert.That(ok.ExitCode).IsEqualTo(0);
        await Assert.That(invalid.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(invalid.StdErr)).IsEqualTo("Invalid token.");
        await Assert.That(missing.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missing.StdErr)).IsEqualTo("Capability token is required.");

        using var verifyCtx = CreateContext();
        var verifyConn = verifyCtx.Connection;
        var replyId = Scalar<long>(verifyConn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var replyToId = Scalar<long>(verifyConn, $"SELECT reply_to_id FROM messages WHERE id = {replyId}");
        var aliceRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {replyId} AND recipient_id = 'alice'");
        var carolRecipientCount = Scalar<long>(verifyConn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {replyId} AND recipient_id = 'carol'");

        await Assert.That(replyToId).IsEqualTo(originalId);
        await Assert.That(aliceRecipientCount).IsEqualTo(1L);
        await Assert.That(carolRecipientCount).IsEqualTo(0L);
    }

    [Test]
    public async Task Agents_RemainsPublicAndOnlyReturnsActiveAgents()
    {
        await RegisterAgentAsync("active-agent");
        await RegisterAgentAsync("deleted-agent");
        await InvokeAsync("deregister", "deleted-agent");

        var result = await InvokeAsync("agents", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(result.StdOut).Length).IsEqualTo(1);

        using var ctx = CreateContext();
        var conn = ctx.Connection;
        var count = Scalar<long>(conn, "SELECT COUNT(*) FROM agents WHERE deregistered_at IS NULL");
        await Assert.That(count).IsEqualTo(1L);
    }

    private async Task<RegistrationInfo> RegisterAgentAsync(string agentId, string? displayName = null)
    {
        var args = new List<string> { "register", agentId, "--format", "json" };
        if (displayName is not null)
        {
            args.Add("--display-name");
            args.Add(displayName);
        }

        var result = await InvokeAsync(args.ToArray());
        await Assert.That(result.ExitCode).IsEqualTo(0);
        return ParseRegistration(result.StdOut);
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

    private static RegistrationInfo ParseRegistration(string stdout)
    {
        var json = ParseJson(stdout);
        return new RegistrationInfo(
            json.GetProperty("message").GetString() ?? "",
            json.GetProperty("agentId").GetString() ?? "",
            json.GetProperty("capabilityToken").GetString() ?? "");
    }

    private static string ParseError(string stderr) =>
        ParseJson(stderr).GetProperty("error").GetString() ?? "";

    private static JsonElement ParseJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static JsonElement[] ParseJsonArray(string text)
    {
        var array = ParseJson(text);
        var values = new JsonElement[array.GetArrayLength()];
        var index = 0;
        foreach (var item in array.EnumerateArray())
            values[index++] = item.Clone();

        return values;
    }

    private static string HashCapabilityToken(string capabilityToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(capabilityToken));
        return Convert.ToHexStringLower(hash);
    }

    private static bool IsVersion7Guid(string capabilityToken) =>
        Guid.TryParse(capabilityToken, out _)
        && capabilityToken.Split('-') is [_, _, var versionSegment, ..]
        && versionSegment.Length > 0
        && versionSegment[0] == '7';

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

    private sealed record RegistrationInfo(string Message, string AgentId, string CapabilityToken);
}
