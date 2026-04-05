using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class GroupCreateCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = CommandNames.Descriptions.GroupIdArg };
        var cmd = new Command(CommandNames.GroupCreate, CommandNames.Descriptions.GroupCreate) { groupIdArg };

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

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT deleted_at FROM groups WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", groupId);
                var deletedAt = checkCmd.ExecuteScalar();

                if (deletedAt is null)
                {
                    // Group does not exist, create it
                    using var insertCmd = conn.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO groups (id) VALUES (@id)";
                    insertCmd.Parameters.AddWithValue("@id", groupId);
                    insertCmd.ExecuteNonQuery();

                    formatter.WriteSuccess(CommandNames.Messages.GroupCreated(groupId));
                    return 0;
                }

                if (deletedAt is DBNull)
                {
                    // Group exists and is active
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupAlreadyExists(groupId));
                }

                // Group exists but is soft-deleted, reactivate it
                using var reactivateCmd = conn.CreateCommand();
                reactivateCmd.CommandText = "UPDATE groups SET deleted_at = NULL, created_at = datetime('now') WHERE id = @id";
                reactivateCmd.Parameters.AddWithValue("@id", groupId);
                reactivateCmd.ExecuteNonQuery();

                formatter.WriteSuccess(CommandNames.Messages.GroupCreated(groupId));
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
