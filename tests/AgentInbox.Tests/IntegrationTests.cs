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
    public async Task Register_PreCapabilityTokenDatabase_ReturnsFreshDatabaseError()
    {
        await CreateLegacySchemaAsync();

        var result = await InvokeAsync("register", "alice", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(result.StdErr)).IsEqualTo(
            "This version requires a fresh database. Migration/backward compatibility is not handled yet.");
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
    public async Task Register_RejectsAgentIdWithGroupPrefix()
    {
        var result = await InvokeAsync("register", "group:engineering", "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(result.StdErr)).IsEqualTo("Agent ID 'group:engineering' cannot start with 'group:' (reserved prefix).");
    }

    [Test]
    public async Task GroupCreate_ReactivatesSoftDeletedGroup()
    {
        var firstCreate = await InvokeAsync("group-create", "engineering", "--format", "json");
        var delete = await InvokeAsync("group-delete", "engineering", "--format", "json");
        var secondCreate = await InvokeAsync("group-create", "engineering", "--format", "json");
        var groups = await InvokeAsync("groups", "--format", "json");

        await Assert.That(firstCreate.ExitCode).IsEqualTo(0);
        await Assert.That(delete.ExitCode).IsEqualTo(0);
        await Assert.That(secondCreate.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(groups.StdOut).Length).IsEqualTo(1);
    }

    [Test]
    public async Task Groups_CanBeManaged_AndListed()
    {
        await RegisterAgentAsync("alice", "Alice");
        await RegisterAgentAsync("bob", "Bob");

        var create = await InvokeAsync("group-create", "engineering", "--format", "json");
        var duplicateCreate = await InvokeAsync("group-create", "engineering", "--format", "json");
        var groups = await InvokeAsync("groups", "--format", "json");
        var addAlice = await InvokeAsync("group-add-member", "engineering", "alice", "--format", "json");
        var addBob = await InvokeAsync("group-add-member", "engineering", "bob", "--format", "json");
        var duplicateMember = await InvokeAsync("group-add-member", "engineering", "alice", "--format", "json");
        var members = await InvokeAsync("group-members", "engineering", "--format", "json");
        var removeAlice = await InvokeAsync("group-remove-member", "engineering", "alice", "--format", "json");
        var removeAliceAgain = await InvokeAsync("group-remove-member", "engineering", "alice", "--format", "json");
        var delete = await InvokeAsync("group-delete", "engineering", "--format", "json");
        var deleteMissing = await InvokeAsync("group-delete", "engineering", "--format", "json");

        await Assert.That(create.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(groups.StdOut).Length).IsEqualTo(1);
        await Assert.That(addAlice.ExitCode).IsEqualTo(0);
        await Assert.That(addBob.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJson(members.StdOut).GetProperty("members").GetArrayLength()).IsEqualTo(2);
        await Assert.That(removeAlice.ExitCode).IsEqualTo(0);
        await Assert.That(delete.ExitCode).IsEqualTo(0);

        await Assert.That(duplicateCreate.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(duplicateCreate.StdErr)).IsEqualTo("Group 'engineering' already exists.");
        await Assert.That(duplicateMember.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(duplicateMember.StdErr)).IsEqualTo("Agent 'alice' is already a member of group 'engineering'.");
        await Assert.That(removeAliceAgain.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(removeAliceAgain.StdErr)).IsEqualTo("Agent 'alice' is not a member of group 'engineering'.");
        await Assert.That(deleteMissing.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(deleteMissing.StdErr)).IsEqualTo("Group 'engineering' not found.");
    }

}
