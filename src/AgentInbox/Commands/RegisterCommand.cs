using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class RegisterCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var agentIdArg = new Argument<string>("agent-id", "The unique agent identifier");
        var displayNameOpt = new Option<string?>("--display-name", "Optional display name for the agent");

        var cmd = new Command("register", "Register an agent")
        {
            agentIdArg,
            displayNameOpt
        };

        cmd.SetHandler((string agentId, string? displayName, string dbPath, OutputFormat format) =>
        {
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT id, deregistered_at FROM agents WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", agentId);
                using var reader = checkCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    using var insertCmd = conn.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO agents (id, display_name) VALUES (@id, @displayName)";
                    insertCmd.Parameters.AddWithValue("@id", agentId);
                    insertCmd.Parameters.AddWithValue("@displayName", (object?)displayName ?? DBNull.Value);
                    insertCmd.ExecuteNonQuery();
                    formatter.WriteSuccess($"Agent '{agentId}' registered successfully.");
                }
                else
                {
                    bool isDeregistered = !reader.IsDBNull(1);
                    reader.Close();

                    if (!isDeregistered)
                    {
                        formatter.WriteSuccess($"Agent '{agentId}' is already registered.");
                        return;
                    }

                    using var reactivateCmd = conn.CreateCommand();
                    reactivateCmd.CommandText = "UPDATE agents SET deregistered_at = NULL, display_name = @displayName, registered_at = datetime('now') WHERE id = @id";
                    reactivateCmd.Parameters.AddWithValue("@id", agentId);
                    reactivateCmd.Parameters.AddWithValue("@displayName", (object?)displayName ?? DBNull.Value);
                    reactivateCmd.ExecuteNonQuery();
                    formatter.WriteSuccess($"Agent '{agentId}' reactivated.");
                }
            }
            catch (Exception ex)
            {
                formatter.WriteError(ex.Message);
                Environment.Exit(1);
            }
        }, agentIdArg, displayNameOpt, dbPathOption, formatOption);

        return cmd;
    }
}
