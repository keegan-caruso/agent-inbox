using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class AgentsCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var cmd = new Command("agents", "List all active agents");

        cmd.SetHandler((string dbPath, OutputFormat format) =>
        {
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var listCmd = conn.CreateCommand();
                listCmd.CommandText = "SELECT id, display_name, registered_at FROM agents WHERE deregistered_at IS NULL ORDER BY registered_at";
                using var reader = listCmd.ExecuteReader();

                var agents = new List<Agent>();
                while (reader.Read())
                {
                    agents.Add(new Agent
                    {
                        Id = reader.GetString(0),
                        DisplayName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        RegisteredAt = reader.GetString(2)
                    });
                }

                formatter.WriteAgents(agents);
            }
            catch (Exception ex)
            {
                formatter.WriteError(ex.Message);
                Environment.Exit(1);
            }
        }, dbPathOption, formatOption);

        return cmd;
    }
}
