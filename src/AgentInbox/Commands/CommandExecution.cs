using System.CommandLine;
using AgentInbox.Formatters;
using AgentInbox.Security;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Commands;

internal static class CommandExecution
{
    public static int Fail(IOutputFormatter formatter, string message)
    {
        formatter.WriteError(message);
        return 1;
    }

    public static int Fail(IOutputFormatter formatter, Exception exception) =>
        Fail(formatter, exception.Message);

    public static string? ResolveCapabilityToken(ParseResult parseResult, Option<string?> tokenOption)
    {
        var cliToken = parseResult.GetValue(tokenOption);
        return cliToken ?? Environment.GetEnvironmentVariable(CommandNames.CapabilityTokenEnvVar);
    }

    public static bool TryResolveActiveAgentId(
        SqliteConnection conn,
        ParseResult parseResult,
        Option<string?> tokenOption,
        IOutputFormatter formatter,
        out string agentId)
    {
        var capabilityToken = ResolveCapabilityToken(parseResult, tokenOption);
        if (string.IsNullOrWhiteSpace(capabilityToken))
        {
            agentId = string.Empty;
            Fail(formatter, CommandNames.Messages.CapabilityTokenRequired);
            return false;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id
            FROM agents
            WHERE capability_token_hash = @capabilityTokenHash
              AND deregistered_at IS NULL
            """;
        cmd.Parameters.AddWithValue("@capabilityTokenHash", CapabilityTokens.Hash(capabilityToken));

        if (cmd.ExecuteScalar() is not string resolvedAgentId)
        {
            agentId = string.Empty;
            Fail(formatter, CommandNames.Messages.InvalidCapabilityToken);
            return false;
        }

        agentId = resolvedAgentId;
        return true;
    }

    public static bool IsActiveAgent(SqliteConnection conn, string agentId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM agents WHERE id = @id AND deregistered_at IS NULL";
        cmd.Parameters.AddWithValue("@id", agentId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public static HashSet<string> GetActiveAgentIds(SqliteConnection conn, IReadOnlyList<string> agentIds)
    {
        var activeAgentIds = new HashSet<string>(StringComparer.Ordinal);
        if (agentIds.Count == 0)
            return activeAgentIds;

        using var cmd = conn.CreateCommand();
        var parameterNames = new List<string>(agentIds.Count);
        for (var i = 0; i < agentIds.Count; i++)
        {
            var parameterName = $"@id{i}";
            parameterNames.Add(parameterName);
            cmd.Parameters.AddWithValue(parameterName, agentIds[i]);
        }

        cmd.CommandText =
            $"SELECT id FROM agents WHERE deregistered_at IS NULL AND id IN ({string.Join(", ", parameterNames)})";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            activeAgentIds.Add(reader.GetString(0));

        return activeAgentIds;
    }
}
