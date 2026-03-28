using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentInbox;
using AgentInbox.Database;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Tests;

public sealed partial class IntegrationTests
{
    private async Task CreateLegacySchemaAsync()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath
        };

        await using var connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE agents (
                id              TEXT PRIMARY KEY,
                display_name    TEXT,
                registered_at   TEXT NOT NULL DEFAULT (datetime('now')),
                deregistered_at TEXT
            );

            CREATE TABLE messages (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                sender_id    TEXT NOT NULL REFERENCES agents(id),
                subject      TEXT,
                body         TEXT NOT NULL,
                reply_to_id  INTEGER REFERENCES messages(id),
                created_at   TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE message_recipients (
                message_id   INTEGER NOT NULL REFERENCES messages(id),
                recipient_id TEXT    NOT NULL REFERENCES agents(id),
                is_read      INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (message_id, recipient_id)
            );
            """;
        await command.ExecuteNonQueryAsync();
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
