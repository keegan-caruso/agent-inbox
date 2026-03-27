using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class RegisterCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = CommandNames.Descriptions.AgentIdArg };
        var displayNameOpt = new Option<string?>(CommandNames.DisplayName) { Description = CommandNames.Descriptions.DisplayName };

        var cmd = new Command(CommandNames.Register, CommandNames.Descriptions.Register)
        {
            agentIdArg,
            displayNameOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var agentId = parseResult.GetValue(agentIdArg)!;
            var displayName = parseResult.GetValue(displayNameOpt);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
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
        });

        return cmd;
    }
}
