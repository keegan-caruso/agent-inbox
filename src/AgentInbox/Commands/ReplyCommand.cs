using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class ReplyCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };
        var toMessageOpt = new Option<long>(CommandNames.ToMessage) { Required = true, Description = CommandNames.Descriptions.ToMessage };
        var bodyOpt = new Option<string>(CommandNames.Body) { Required = true, Description = CommandNames.Descriptions.ReplyBody };

        var cmd = new Command(CommandNames.Reply, CommandNames.Descriptions.Reply)
        {
            tokenOpt,
            toMessageOpt,
            bodyOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var toMessage = parseResult.GetValue(toMessageOpt);
            var body = parseResult.GetValue(bodyOpt)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var senderId))
                    return 1;

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = "SELECT id, sender_id, subject FROM messages WHERE id = @id";
                msgCmd.Parameters.AddWithValue("@id", toMessage);
                using var msgReader = msgCmd.ExecuteReader();
                if (!msgReader.Read())
                    return CommandExecution.Fail(formatter, CommandNames.Messages.MessageNotFound(toMessage));
                string originalSenderId = msgReader.GetString(1);
                string? originalSubject = msgReader.IsDBNull(2) ? null : msgReader.GetString(2);
                msgReader.Close();

                using var recipientsCmd = conn.CreateCommand();
                recipientsCmd.CommandText = "SELECT recipient_id FROM message_recipients WHERE message_id = @id";
                recipientsCmd.Parameters.AddWithValue("@id", toMessage);
                using var recipientsReader = recipientsCmd.ExecuteReader();
                var originalRecipients = new List<string>();
                while (recipientsReader.Read())
                    originalRecipients.Add(recipientsReader.GetString(0));
                recipientsReader.Close();

                if (senderId != originalSenderId && !originalRecipients.Contains(senderId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.SenderNotParticipant(senderId, toMessage));

                var activeParticipantIds = CommandExecution.GetActiveAgentIds(conn, originalRecipients);
                var replyRecipients = new HashSet<string>(activeParticipantIds, StringComparer.Ordinal);

                if (CommandExecution.IsActiveAgent(conn, originalSenderId))
                    replyRecipients.Add(originalSenderId);

                replyRecipients.Remove(senderId);

                if (replyRecipients.Count == 0)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.NoReplyRecipients);

                using var tx = conn.BeginTransaction();

                string? replySubject = originalSubject != null && !originalSubject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase)
                    ? $"Re: {originalSubject}"
                    : originalSubject;

                using var insertMsgCmd = conn.CreateCommand();
                insertMsgCmd.Transaction = tx;
                insertMsgCmd.CommandText = "INSERT INTO messages (sender_id, subject, body, reply_to_id) VALUES (@senderId, @subject, @body, @replyToId); SELECT last_insert_rowid();";
                insertMsgCmd.Parameters.AddWithValue("@senderId", senderId);
                insertMsgCmd.Parameters.AddWithValue("@subject", (object?)replySubject ?? DBNull.Value);
                insertMsgCmd.Parameters.AddWithValue("@body", body);
                insertMsgCmd.Parameters.AddWithValue("@replyToId", toMessage);
                var newMessageId = (long)(insertMsgCmd.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert reply"));

                using var insertRecCmd = conn.CreateCommand();
                insertRecCmd.Transaction = tx;
                insertRecCmd.CommandText = "INSERT INTO message_recipients (message_id, recipient_id) VALUES (@messageId, @recipientId)";
                insertRecCmd.Parameters.AddWithValue("@messageId", newMessageId);
                var recipientIdParam = insertRecCmd.Parameters.AddWithValue("@recipientId", DBNull.Value);

                foreach (var recipientId in replyRecipients)
                {
                    recipientIdParam.Value = recipientId;
                    insertRecCmd.ExecuteNonQuery();
                }

                tx.Commit();
                formatter.WriteSuccess(CommandNames.Messages.ReplySent(newMessageId));
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
