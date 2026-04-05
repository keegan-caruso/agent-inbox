namespace AgentInbox.Tests;

public sealed partial class IntegrationTests
{
    [Test]
    public async Task Search_TextMode_FindsMatchingMessages()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--subject", "Deployment issue", "--body", "The production deployment failed due to a timeout.");
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--subject", "Meeting tomorrow", "--body", "Let's meet at 10am to discuss the roadmap.");

        var result = await InvokeAsync("search", "--token", bob.CapabilityToken, "--query", "deployment", "--format", "json");
        var noResult = await InvokeAsync("search", "--token", bob.CapabilityToken, "--query", "xyzzy_no_match_at_all", "--format", "json");
        var missingToken = await InvokeAsync("search", "--query", "deployment", "--format", "json");
        var missingQuery = await InvokeAsync("search", "--token", bob.CapabilityToken, "--format", "json");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var results = ParseJsonArray(result.StdOut);
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].GetProperty("subject").GetString()).IsEqualTo("Deployment issue");

        await Assert.That(noResult.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(noResult.StdOut).Length).IsEqualTo(0);

        await Assert.That(missingToken.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missingToken.StdErr)).IsEqualTo("Capability token is required.");

        await Assert.That(missingQuery.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(missingQuery.StdErr)).IsEqualTo("A --query is required.");
    }

    [Test]
    public async Task Search_TextMode_IsScoped_ToRecipientInbox()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");

        // Alice sends "deployment" to Bob only — Carol should NOT see it
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--subject", "Deployment", "--body", "The deployment is done.");

        var bobSearch = await InvokeAsync("search", "--token", bob.CapabilityToken, "--query", "deployment", "--format", "json");
        var carolSearch = await InvokeAsync("search", "--token", carol.CapabilityToken, "--query", "deployment", "--format", "json");

        await Assert.That(bobSearch.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(bobSearch.StdOut).Length).IsEqualTo(1);

        await Assert.That(carolSearch.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(carolSearch.StdOut).Length).IsEqualTo(0);
    }

    [Test]
    public async Task Search_TextMode_RespectsLimit()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");

        for (var i = 1; i <= 5; i++)
            await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", $"hello world message {i}");

        var limitedResult = await InvokeAsync("search", "--token", bob.CapabilityToken, "--query", "hello", "--limit", "2", "--format", "json");
        var allResult = await InvokeAsync("search", "--token", bob.CapabilityToken, "--query", "hello", "--format", "json");

        await Assert.That(limitedResult.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(limitedResult.StdOut).Length).IsEqualTo(2);

        await Assert.That(allResult.ExitCode).IsEqualTo(0);
        await Assert.That(ParseJsonArray(allResult.StdOut).Length).IsEqualTo(5);
    }

    [Test]
    public async Task Index_And_SemanticSearch_WorkTogether()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--subject", "Deploy prod", "--body", "Deploying to production at 5pm.");
        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--subject", "Lunch plans", "--body", "Want to grab lunch tomorrow?");

        long deployMsgId, lunchMsgId;
        using (var ctx = CreateContext())
        {
            var conn = ctx.Connection;
            deployMsgId = Scalar<long>(conn, "SELECT id FROM messages WHERE subject = 'Deploy prod'");
            lunchMsgId = Scalar<long>(conn, "SELECT id FROM messages WHERE subject = 'Lunch plans'");
        }

        // Index both messages using built-in embedding generation
        var indexDeploy = await InvokeAsync("index", deployMsgId.ToString(), "--token", bob.CapabilityToken, "--format", "json");
        var indexLunch = await InvokeAsync("index", lunchMsgId.ToString(), "--token", bob.CapabilityToken, "--format", "json");

        await Assert.That(indexDeploy.ExitCode).IsEqualTo(0);
        await Assert.That(indexLunch.ExitCode).IsEqualTo(0);

        // Semantic search for "production deployment"
        var searchResult = await InvokeAsync("search", "--token", bob.CapabilityToken, "--mode", "semantic", "--query", "production deployment", "--limit", "1", "--format", "json");

        await Assert.That(searchResult.ExitCode).IsEqualTo(0);
        var results = ParseJsonArray(searchResult.StdOut);
        await Assert.That(results.Length).IsEqualTo(1);
        // The deployment message should score better than the lunch message
        await Assert.That(results[0].GetProperty("subject").GetString()).IsEqualTo("Deploy prod");
    }

    [Test]
    public async Task Index_RequiresParticipantAccess()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");
        var carol = await RegisterAgentAsync("carol");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", "private message");

        long msgId;
        using (var ctx = CreateContext())
            msgId = Scalar<long>(ctx.Connection, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");

        // Carol is not a participant — should fail
        var denied = await InvokeAsync("index", msgId.ToString(), "--token", carol.CapabilityToken, "--format", "json");
        // Sender (alice) can also index
        var senderOk = await InvokeAsync("index", msgId.ToString(), "--token", alice.CapabilityToken, "--format", "json");
        // Recipient (bob) can index
        var recipientOk = await InvokeAsync("index", msgId.ToString(), "--token", bob.CapabilityToken, "--format", "json");

        await Assert.That(denied.ExitCode).IsEqualTo(1);
        await Assert.That(ParseError(denied.StdErr)).IsEqualTo($"Message {msgId} not found or you are not a participant.");
        await Assert.That(senderOk.ExitCode).IsEqualTo(0);
        await Assert.That(recipientOk.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Index_WithCustomEmbedding_StoresAndSearches()
    {
        var alice = await RegisterAgentAsync("alice");
        var bob = await RegisterAgentAsync("bob");

        await InvokeAsync("send", "--token", alice.CapabilityToken, "--to", "bob", "--body", "test message");

        long msgId;
        using (var ctx = CreateContext())
            msgId = Scalar<long>(ctx.Connection, "SELECT id FROM messages ORDER BY id DESC LIMIT 1");

        // Create a 384-dimensional embedding as a JSON string (AOT-safe manual construction)
        // Hot-bit at index 7, all others zero.
        var embeddingParts = new string[384];
        for (var j = 0; j < 384; j++)
            embeddingParts[j] = j == 7 ? "1.0" : "0.0";
        var embeddingJson = "[" + string.Join(",", embeddingParts) + "]";

        var indexResult = await InvokeAsync("index", msgId.ToString(), "--token", bob.CapabilityToken, "--embedding", embeddingJson, "--format", "json");
        await Assert.That(indexResult.ExitCode).IsEqualTo(0);

        // Search with the same embedding should find the message
        var searchResult = await InvokeAsync("search", "--token", bob.CapabilityToken, "--mode", "semantic", "--embedding", embeddingJson, "--format", "json");
        await Assert.That(searchResult.ExitCode).IsEqualTo(0);
        var results = ParseJsonArray(searchResult.StdOut);
        await Assert.That(results.Length).IsEqualTo(1);
    }
}
