using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class GroupMembersCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = CommandNames.Descriptions.GroupIdArg };
        var cmd = new Command(CommandNames.GroupMembers, CommandNames.Descriptions.GroupMembers) { groupIdArg };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var groupId = parseResult.GetValue(groupIdArg)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.GroupExists(conn, groupId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupNotFound(groupId));

                using var listCmd = conn.CreateCommand();
                listCmd.CommandText = """
                    SELECT a.id, a.display_name
                    FROM group_members gm
                    JOIN agents a ON a.id = gm.agent_id
                    WHERE gm.group_id = @groupId
                    ORDER BY a.id
                    """;
                listCmd.Parameters.AddWithValue("@groupId", groupId);
                using var reader = listCmd.ExecuteReader();

                var members = new List<GroupMember>();
                while (reader.Read())
                {
                    members.Add(new GroupMember
                    {
                        AgentId = reader.GetString(0),
                        DisplayName = reader.IsDBNull(1) ? null : reader.GetString(1)
                    });
                }

                formatter.WriteGroupMembers(groupId, members);
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
