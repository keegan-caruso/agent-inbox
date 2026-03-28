namespace AgentInbox.Tests;

public sealed partial class IntegrationTests
{
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

    [Test]
    public async Task Send_SupportsGroups_AndDedupesWithDirectRecipients()
    {
        var alice = await RegisterAgentAsync("alice");
        await RegisterAgentAsync("bob");
        await RegisterAgentAsync("carol");

        await InvokeAsync("group-create", "engineering");
        await InvokeAsync("group-add-member", "engineering", "bob");
        await InvokeAsync("group-add-member", "engineering", "carol");

        var send = await InvokeAsync(
            "send",
            "--token", alice.CapabilityToken,
            "--to", "bob,group:engineering,group:engineering",
            "--body", "hello");

        await Assert.That(send.ExitCode).IsEqualTo(0);

        using var verifyCtx = CreateContext();
        var conn = verifyCtx.Connection;
        var messageId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var recipientCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {messageId}");
        var bobCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {messageId} AND recipient_id = 'bob'");
        var carolCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {messageId} AND recipient_id = 'carol'");

        await Assert.That(recipientCount).IsEqualTo(2L);
        await Assert.That(bobCount).IsEqualTo(1L);
        await Assert.That(carolCount).IsEqualTo(1L);
    }

    [Test]
    public async Task Send_ToGroups_FailsForMissingOrEmptyGroup_AndSkipsInactiveMembers()
    {
        var alice = await RegisterAgentAsync("alice");
        await RegisterAgentAsync("bob");
        await RegisterAgentAsync("carol");

        var missingGroup = await InvokeAsync(
            "send",
            "--token", alice.CapabilityToken,
            "--to", "group:does-not-exist",
            "--body", "hello",
            "--format", "json");
        await Assert.That(missingGroup.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missingGroup.StdErr)).IsEqualTo("Group 'does-not-exist' not found.");

        await InvokeAsync("group-create", "inactive-only");
        await InvokeAsync("group-add-member", "inactive-only", "carol");
        await InvokeAsync("deregister", "carol");
        var emptyActive = await InvokeAsync(
            "send",
            "--token", alice.CapabilityToken,
            "--to", "group:inactive-only",
            "--body", "hello",
            "--format", "json");
        await Assert.That(emptyActive.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(emptyActive.StdErr)).IsEqualTo("Group 'inactive-only' has no active members.");

        await InvokeAsync("group-create", "mixed-members");
        await InvokeAsync("group-add-member", "mixed-members", "bob");
        await InvokeAsync("group-add-member", "mixed-members", "carol");
        var mixedSend = await InvokeAsync(
            "send",
            "--token", alice.CapabilityToken,
            "--to", "group:mixed-members",
            "--body", "hello");
        await Assert.That(mixedSend.ExitCode).IsEqualTo(0);

        using var verifyCtx = CreateContext();
        var conn = verifyCtx.Connection;
        var messageId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var bobCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {messageId} AND recipient_id = 'bob'");
        var carolCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {messageId} AND recipient_id = 'carol'");
        await Assert.That(bobCount).IsEqualTo(1L);
        await Assert.That(carolCount).IsEqualTo(0L);
    }

    [Test]
    public async Task Reply_AfterGroupDelivery_KeepsParticipantSemantics()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        await RegisterAgentAsync("carol");

        await InvokeAsync("group-create", "engineering");
        await InvokeAsync("group-add-member", "engineering", "bob");
        await InvokeAsync("group-add-member", "engineering", "carol");
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "group:engineering", "--body", "original");

        var originalId = 0L;
        using (var ctx = CreateContext())
        {
            originalId = Scalar<long>(ctx.Connection, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        }

        var reply = await InvokeAsync("reply", "--token", bob.CapabilityToken, "--to-message", originalId.ToString(), "--body", "reply");
        await Assert.That(reply.ExitCode).IsEqualTo(0);

        using var verifyCtx = CreateContext();
        var conn = verifyCtx.Connection;
        var replyId = Scalar<long>(conn, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");
        var replyRecipientCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {replyId}");
        var aliceCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {replyId} AND recipient_id = 'alice'");
        var carolCount = Scalar<long>(conn, $"SELECT COUNT(*) FROM message_recipients WHERE message_id = {replyId} AND recipient_id = 'carol'");
        await Assert.That(replyRecipientCount).IsEqualTo(2L);
        await Assert.That(aliceCount).IsEqualTo(1L);
        await Assert.That(carolCount).IsEqualTo(1L);
    }
}
