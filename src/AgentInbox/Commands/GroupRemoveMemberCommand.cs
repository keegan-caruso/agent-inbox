using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class GroupRemoveMemberCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = CommandNames.Descriptions.GroupIdArg };
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = CommandNames.Descriptions.AgentIdArg };
        var cmd = new Command(CommandNames.GroupRemoveMember, CommandNames.Descriptions.GroupRemoveMember) { groupIdArg, agentIdArg };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var groupId = parseResult.GetValue(groupIdArg)!;
            var agentId = parseResult.GetValue(agentIdArg)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.GroupExists(conn, groupId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupNotFound(groupId));

                using var deleteCmd = conn.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM group_members WHERE group_id = @groupId AND agent_id = @agentId";
                deleteCmd.Parameters.AddWithValue("@groupId", groupId);
                deleteCmd.Parameters.AddWithValue("@agentId", agentId);
                var rows = deleteCmd.ExecuteNonQuery();
                if (rows == 0)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupMemberNotFound(groupId, agentId));

                formatter.WriteSuccess(CommandNames.Messages.GroupMemberRemoved(groupId, agentId));
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
