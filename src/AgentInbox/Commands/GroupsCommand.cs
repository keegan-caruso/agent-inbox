using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class GroupsCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var cmd = new Command(CommandNames.Groups, CommandNames.Descriptions.Groups);

        cmd.SetAction((ParseResult parseResult) =>
        {
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var listCmd = conn.CreateCommand();
                listCmd.CommandText = "SELECT id, created_at FROM groups WHERE deleted_at IS NULL ORDER BY created_at, id";
                using var reader = listCmd.ExecuteReader();

                var groups = new List<Group>();
                while (reader.Read())
                {
                    groups.Add(new Group(
                        reader.GetString(0),
                        reader.GetString(1)));
                }

                formatter.WriteGroups(groups);
                return 0;
            }
            catch (Exception ex)
            {
                return CommandExecution.Fail(formatter, ex);
            }
        });

        return cmd;
    }
}
