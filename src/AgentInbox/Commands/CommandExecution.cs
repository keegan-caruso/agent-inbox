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
}
