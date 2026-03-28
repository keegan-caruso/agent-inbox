using AgentInbox.Database;
using TUnit.Core;

namespace AgentInbox.Tests;

[NotInParallel]
public sealed partial class IntegrationTests : IDisposable
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
    public async Task FreshDb_SetsUserVersionToCurrentSchemaVersion()
    {
        await InvokeAsync("register", "alice");

        var version = await GetUserVersionAsync(_dbPath);

        await Assert.That(version).IsEqualTo(DbBootstrap.CurrentSchemaVersion);
    }

    [Test]
    public async Task CurrentVersionDb_OpensSuccessfully()
    {
        var first = await InvokeAsync("register", "alice");
        await Assert.That(first.ExitCode).IsEqualTo(0);

        var second = await InvokeAsync("agents", "--format", "json");
        await Assert.That(second.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task UnversionedLegacyDb_FailsWithMigrationMessage()
    {
        await CreateLegacySchemaAsync();

        var result = await InvokeAsync("register", "alice", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(result.StdErr)).IsEqualTo(
            "This database uses an unversioned legacy schema. Migration is not implemented yet.");
    }

    [Test]
    public async Task OlderVersionedDb_FailsWithMigrationMessage()
    {
        await CreateDbWithVersionAsync(1);

        var result = await InvokeAsync("register", "alice", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(result.StdErr)).IsEqualTo(
            $"Database schema version 1 is older than supported version {DbBootstrap.CurrentSchemaVersion}. Migration is not implemented yet.");
    }

    [Test]
    public async Task NewerVersionedDb_FailsWithNewerSchemaMessage()
    {
        var newerVersion = DbBootstrap.CurrentSchemaVersion + 1;
        await CreateDbWithVersionAsync(newerVersion);

        var result = await InvokeAsync("register", "alice", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(result.StdErr)).IsEqualTo(
            $"Database schema version {newerVersion} is newer than this application supports ({DbBootstrap.CurrentSchemaVersion}).");
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

}
