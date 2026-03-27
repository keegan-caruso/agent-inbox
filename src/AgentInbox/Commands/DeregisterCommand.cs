using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class DeregisterCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = "The unique agent identifier" };

        var cmd = new Command(CommandNames.Deregister, "Deregister (soft-delete) an agent")
        {
            agentIdArg
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var agentId = parseResult.GetValue(agentIdArg)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT deregistered_at FROM agents WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", agentId);
                using var reader = checkCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    formatter.WriteError($"Agent '{agentId}' not found.");
                    Environment.Exit(1);
                    return;
                }

                bool alreadyDeregistered = !reader.IsDBNull(0);
                reader.Close();

                if (alreadyDeregistered)
                {
                    formatter.WriteError($"Agent '{agentId}' is already deregistered.");
                    Environment.Exit(1);
                    return;
                }

                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = "UPDATE agents SET deregistered_at = datetime('now') WHERE id = @id";
                updateCmd.Parameters.AddWithValue("@id", agentId);
                updateCmd.ExecuteNonQuery();
                formatter.WriteSuccess($"Agent '{agentId}' deregistered.");
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
