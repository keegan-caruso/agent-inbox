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

                using var existsCmd = conn.CreateCommand();
                existsCmd.CommandText = "SELECT COUNT(*) FROM groups WHERE id = @id AND deleted_at IS NULL";
                existsCmd.Parameters.AddWithValue("@id", groupId);
                if ((long)(existsCmd.ExecuteScalar() ?? 0L) > 0)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupAlreadyExists(groupId));

                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = "INSERT INTO groups (id) VALUES (@id)";
                insertCmd.Parameters.AddWithValue("@id", groupId);
                insertCmd.ExecuteNonQuery();

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
