using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class GroupAddMemberCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = CommandNames.Descriptions.GroupIdArg };
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = CommandNames.Descriptions.AgentIdArg };
        var cmd = new Command(CommandNames.GroupAddMember, CommandNames.Descriptions.GroupAddMember) { groupIdArg, agentIdArg };

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

                if (!CommandExecution.AgentExists(conn, agentId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.AgentNotFound(agentId));

                using var existsCmd = conn.CreateCommand();
                existsCmd.CommandText = "SELECT COUNT(*) FROM group_members WHERE group_id = @groupId AND agent_id = @agentId";
                existsCmd.Parameters.AddWithValue("@groupId", groupId);
                existsCmd.Parameters.AddWithValue("@agentId", agentId);
                if ((long)(existsCmd.ExecuteScalar() ?? 0L) > 0)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupMemberAlreadyExists(groupId, agentId));

                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = "INSERT INTO group_members (group_id, agent_id) VALUES (@groupId, @agentId)";
                insertCmd.Parameters.AddWithValue("@groupId", groupId);
                insertCmd.Parameters.AddWithValue("@agentId", agentId);
                insertCmd.ExecuteNonQuery();

                formatter.WriteSuccess(CommandNames.Messages.GroupMemberAdded(groupId, agentId));
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
