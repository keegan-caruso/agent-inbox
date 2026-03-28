using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class ReadCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var messageIdArg = new Argument<long>(CommandNames.MessageIdArg) { Description = CommandNames.Descriptions.MessageIdArg };
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };

        var cmd = new Command(CommandNames.Read, CommandNames.Descriptions.Read)
        {
            messageIdArg,
            tokenOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var messageId = parseResult.GetValue(messageIdArg);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var agentId))
                    return 1;

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = """
                    SELECT m.id, m.sender_id, m.subject, m.body, m.reply_to_id, m.created_at
                    FROM messages m
                    JOIN message_recipients mr ON mr.message_id = m.id
                    WHERE m.id = @id AND mr.recipient_id = @agentId
                    """;
                msgCmd.Parameters.AddWithValue("@id", messageId);
                msgCmd.Parameters.AddWithValue("@agentId", agentId);
                using var reader = msgCmd.ExecuteReader();

                if (!reader.Read())
                    return CommandExecution.Fail(formatter, CommandNames.Messages.MessageNotAccessible(messageId, agentId));

                var message = new Message
                {
                    Id = reader.GetInt64(0),
                    SenderId = reader.GetString(1),
                    Subject = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Body = reader.GetString(3),
                    ReplyToId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                    CreatedAt = reader.GetString(5)
                };
                reader.Close();

                using var markCmd = conn.CreateCommand();
                markCmd.CommandText = "UPDATE message_recipients SET is_read = 1 WHERE message_id = @messageId AND recipient_id = @agentId";
                markCmd.Parameters.AddWithValue("@messageId", messageId);
                markCmd.Parameters.AddWithValue("@agentId", agentId);
                markCmd.ExecuteNonQuery();

                formatter.WriteMessage(message);
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
