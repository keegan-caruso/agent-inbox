using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class GroupDeleteCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = CommandNames.Descriptions.GroupIdArg };
        var cmd = new Command(CommandNames.GroupDelete, CommandNames.Descriptions.GroupDelete) { groupIdArg };

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

                using var tx = conn.BeginTransaction();

                using var deleteMembersCmd = conn.CreateCommand();
                deleteMembersCmd.Transaction = tx;
                deleteMembersCmd.CommandText = "DELETE FROM group_members WHERE group_id = @groupId";
                deleteMembersCmd.Parameters.AddWithValue("@groupId", groupId);
                deleteMembersCmd.ExecuteNonQuery();

                using var updateGroupCmd = conn.CreateCommand();
                updateGroupCmd.Transaction = tx;
                updateGroupCmd.CommandText = "UPDATE groups SET deleted_at = datetime('now') WHERE id = @id";
                updateGroupCmd.Parameters.AddWithValue("@id", groupId);
                updateGroupCmd.ExecuteNonQuery();

                tx.Commit();
                formatter.WriteSuccess(CommandNames.Messages.GroupDeleted(groupId));
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
