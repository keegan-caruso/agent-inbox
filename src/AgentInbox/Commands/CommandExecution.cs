using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using AgentInbox.Formatters;
using AgentInbox.Security;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Commands;

internal static class CommandExecution
{
    private const string GroupRecipientPrefix = "group:";

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
        [NotNullWhen(true)] out string? agentId)
    {
        var capabilityToken = ResolveCapabilityToken(parseResult, tokenOption);
        if (string.IsNullOrWhiteSpace(capabilityToken))
        {
            agentId = null;
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
            agentId = null;
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

    public static bool GroupExists(SqliteConnection conn, string groupId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM groups WHERE id = @id AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("@id", groupId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public static bool AgentExists(SqliteConnection conn, string agentId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM agents WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", agentId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public static bool TryResolveSendRecipients(
        SqliteConnection conn,
        IReadOnlyList<string> rawRecipients,
        IOutputFormatter formatter,
        [NotNullWhen(true)] out List<string>? resolvedRecipientIds)
    {
        resolvedRecipientIds = null;
        if (rawRecipients.Count == 0)
        {
            Fail(formatter, CommandNames.Messages.NoRecipientsSpecified);
            return false;
        }

        var directRecipientIds = new List<string>();
        var groupIds = new List<string>();

        var seenDirectRecipients = new HashSet<string>(StringComparer.Ordinal);
        var seenGroupIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawRecipient in rawRecipients)
        {
            if (rawRecipient.StartsWith(GroupRecipientPrefix, StringComparison.Ordinal))
            {
                var groupId = rawRecipient[GroupRecipientPrefix.Length..];
                if (seenGroupIds.Add(groupId))
                    groupIds.Add(groupId);
                continue;
            }

            if (seenDirectRecipients.Add(rawRecipient))
                directRecipientIds.Add(rawRecipient);
        }

        var finalRecipientIds = new HashSet<string>(StringComparer.Ordinal);

        var activeDirectRecipientIds = GetActiveAgentIds(conn, directRecipientIds);
        foreach (var recipientId in directRecipientIds)
        {
            if (!activeDirectRecipientIds.Contains(recipientId))
            {
                Fail(formatter, CommandNames.Messages.RecipientNotActive(recipientId));
                return false;
            }
            finalRecipientIds.Add(recipientId);
        }

        foreach (var groupId in groupIds)
        {
            if (!GroupExists(conn, groupId))
            {
                Fail(formatter, CommandNames.Messages.GroupNotFound(groupId));
                return false;
            }

            using var membersCmd = conn.CreateCommand();
            membersCmd.CommandText = """
                SELECT gm.agent_id
                FROM group_members gm
                JOIN agents a ON a.id = gm.agent_id
                WHERE gm.group_id = @groupId
                  AND a.deregistered_at IS NULL
                ORDER BY gm.agent_id
                """;
            membersCmd.Parameters.AddWithValue("@groupId", groupId);
            using var membersReader = membersCmd.ExecuteReader();

            var hasActiveMembers = false;
            while (membersReader.Read())
            {
                hasActiveMembers = true;
                finalRecipientIds.Add(membersReader.GetString(0));
            }

            if (!hasActiveMembers)
            {
                Fail(formatter, CommandNames.Messages.GroupHasNoActiveMembers(groupId));
                return false;
            }
        }

        resolvedRecipientIds = [.. finalRecipientIds];
        return true;
    }
}
