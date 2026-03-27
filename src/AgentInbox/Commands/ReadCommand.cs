using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class ReadCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var messageIdArg = new Argument<long>(CommandNames.MessageIdArg) { Description = "Message ID to read" };
        var asOpt = new Option<string>(CommandNames.As) { Required = true, Description = "Agent ID reading the message" };

        var cmd = new Command(CommandNames.Read, "Read a specific message and mark it as read")
        {
            messageIdArg,
            asOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var messageId = parseResult.GetValue(messageIdArg);
            var asAgent = parseResult.GetValue(asOpt)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = "SELECT id, sender_id, subject, body, reply_to_id, created_at FROM messages WHERE id = @id";
                msgCmd.Parameters.AddWithValue("@id", messageId);
                using var reader = msgCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    formatter.WriteError($"Message {messageId} not found.");
                    Environment.Exit(1);
                    return;
                }

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
                markCmd.Parameters.AddWithValue("@agentId", asAgent);
                markCmd.ExecuteNonQuery();

                formatter.WriteMessage(message);
            }
            catch (Exception ex)
            {
                formatter.WriteError(ex.Message);
                Environment.Exit(1);
            }
        });

        return cmd;
    }
}
