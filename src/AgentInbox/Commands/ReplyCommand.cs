using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class ReplyCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var fromOpt = new Option<string>("--from", "Replying agent ID") { IsRequired = true };
        var toMessageOpt = new Option<long>("--to-message", "Message ID to reply to") { IsRequired = true };
        var bodyOpt = new Option<string>("--body", "Reply body") { IsRequired = true };

        var cmd = new Command("reply", "Reply to a message")
        {
            fromOpt,
            toMessageOpt,
            bodyOpt
        };

        cmd.SetHandler((string from, long toMessage, string body, string dbPath, OutputFormat format) =>
        {
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var senderCheckCmd = conn.CreateCommand();
                senderCheckCmd.CommandText = "SELECT COUNT(*) FROM agents WHERE id = @id AND deregistered_at IS NULL";
                senderCheckCmd.Parameters.AddWithValue("@id", from);
                if ((long)(senderCheckCmd.ExecuteScalar() ?? 0L) == 0)
                {
                    formatter.WriteError($"Sender '{from}' is not an active registered agent.");
                    Environment.Exit(1);
                    return;
                }

                using var msgCmd = conn.CreateCommand();
                msgCmd.CommandText = "SELECT id, sender_id, subject FROM messages WHERE id = @id";
                msgCmd.Parameters.AddWithValue("@id", toMessage);
                using var msgReader = msgCmd.ExecuteReader();
                if (!msgReader.Read())
                {
                    msgReader.Close();
                    formatter.WriteError($"Message {toMessage} not found.");
                    Environment.Exit(1);
                    return;
                }
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

                var replyRecipients = new HashSet<string>(originalRecipients) { originalSenderId };
                replyRecipients.Remove(from);

                if (replyRecipients.Count == 0)
                {
                    formatter.WriteError("No recipients for the reply (you are the only participant).");
                    Environment.Exit(1);
                    return;
                }

                using var tx = conn.BeginTransaction();

                string? replySubject = originalSubject != null && !originalSubject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase)
                    ? $"Re: {originalSubject}"
                    : originalSubject;

                using var insertMsgCmd = conn.CreateCommand();
                insertMsgCmd.Transaction = tx;
                insertMsgCmd.CommandText = "INSERT INTO messages (sender_id, subject, body, reply_to_id) VALUES (@senderId, @subject, @body, @replyToId); SELECT last_insert_rowid();";
                insertMsgCmd.Parameters.AddWithValue("@senderId", from);
                insertMsgCmd.Parameters.AddWithValue("@subject", (object?)replySubject ?? DBNull.Value);
                insertMsgCmd.Parameters.AddWithValue("@body", body);
                insertMsgCmd.Parameters.AddWithValue("@replyToId", toMessage);
                var newMessageId = (long)(insertMsgCmd.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert reply"));

                foreach (var recipientId in replyRecipients)
                {
                    using var insertRecCmd = conn.CreateCommand();
                    insertRecCmd.Transaction = tx;
                    insertRecCmd.CommandText = "INSERT INTO message_recipients (message_id, recipient_id) VALUES (@messageId, @recipientId)";
                    insertRecCmd.Parameters.AddWithValue("@messageId", newMessageId);
                    insertRecCmd.Parameters.AddWithValue("@recipientId", recipientId);
                    insertRecCmd.ExecuteNonQuery();
                }

                tx.Commit();
                formatter.WriteSuccess($"Reply sent (ID: {newMessageId}).");
            }
            catch (Exception ex)
            {
                formatter.WriteError(ex.Message);
                Environment.Exit(1);
            }
        }, fromOpt, toMessageOpt, bodyOpt, dbPathOption, formatOption);

        return cmd;
    }
}
