using System.CommandLine;
using AgentInbox.Formatters;
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
