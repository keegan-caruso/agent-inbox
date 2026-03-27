using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class InboxCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = CommandNames.Descriptions.InboxAgentId };
        var unreadOnlyOpt = new Option<bool>(CommandNames.UnreadOnly) { Description = CommandNames.Descriptions.UnreadOnly };

        var cmd = new Command(CommandNames.Inbox, CommandNames.Descriptions.Inbox)
        {
            agentIdArg,
            unreadOnlyOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var agentId = parseResult.GetValue(agentIdArg)!;
            var unreadOnly = parseResult.GetValue(unreadOnlyOpt);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.IsActiveAgent(conn, agentId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.AgentNotActive(agentId));

                var sql = """
                    SELECT m.id, m.sender_id, m.subject, m.body, m.reply_to_id, m.created_at, mr.is_read
                    FROM message_recipients mr
                    JOIN messages m ON m.id = mr.message_id
                    WHERE mr.recipient_id = @agentId
                    """;
                if (unreadOnly)
                    sql += " AND mr.is_read = 0";
                sql += " ORDER BY m.created_at DESC";

                using var inboxCmd = conn.CreateCommand();
                inboxCmd.CommandText = sql;
                inboxCmd.Parameters.AddWithValue("@agentId", agentId);
                using var reader = inboxCmd.ExecuteReader();

                var entries = new List<InboxEntry>();
                while (reader.Read())
                {
                    entries.Add(new InboxEntry
                    {
                        MessageId = reader.GetInt64(0),
                        SenderId = reader.GetString(1),
                        Subject = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Body = reader.GetString(3),
                        ReplyToId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                        CreatedAt = reader.GetString(5),
                        IsRead = reader.GetInt32(6) != 0
                    });
                }

                formatter.WriteInbox(entries);
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
