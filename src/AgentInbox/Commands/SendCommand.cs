using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class SendCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var fromOpt = new Option<string>(CommandNames.From) { Required = true, Description = "Sender agent ID" };
        var toOpt = new Option<string>(CommandNames.To) { Required = true, Description = "Comma-separated recipient agent IDs" };
        var subjectOpt = new Option<string?>(CommandNames.Subject) { Description = "Message subject" };
        var bodyOpt = new Option<string>(CommandNames.Body) { Required = true, Description = "Message body" };

        var cmd = new Command(CommandNames.Send, "Send a message")
        {
            fromOpt,
            toOpt,
            subjectOpt,
            bodyOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var from = parseResult.GetValue(fromOpt)!;
            var to = parseResult.GetValue(toOpt)!;
            var subject = parseResult.GetValue(subjectOpt);
            var body = parseResult.GetValue(bodyOpt)!;
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                var recipientIds = to.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (recipientIds.Length == 0)
                {
                    formatter.WriteError("No recipients specified.");
                    Environment.Exit(1);
                    return;
                }

                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!IsActiveAgent(conn, from))
                {
                    formatter.WriteError($"Sender '{from}' is not an active registered agent.");
                    Environment.Exit(1);
                    return;
                }

                foreach (var recipientId in recipientIds)
                {
                    if (!IsActiveAgent(conn, recipientId))
                    {
                        formatter.WriteError($"Recipient '{recipientId}' is not an active registered agent.");
                        Environment.Exit(1);
                        return;
                    }
                }

                using var tx = conn.BeginTransaction();

                using var insertMsgCmd = conn.CreateCommand();
                insertMsgCmd.Transaction = tx;
                insertMsgCmd.CommandText = "INSERT INTO messages (sender_id, subject, body) VALUES (@senderId, @subject, @body); SELECT last_insert_rowid();";
                insertMsgCmd.Parameters.AddWithValue("@senderId", from);
                insertMsgCmd.Parameters.AddWithValue("@subject", (object?)subject ?? DBNull.Value);
                insertMsgCmd.Parameters.AddWithValue("@body", body);
                var messageId = (long)(insertMsgCmd.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert message"));

                foreach (var recipientId in recipientIds)
                {
                    using var insertRecCmd = conn.CreateCommand();
                    insertRecCmd.Transaction = tx;
                    insertRecCmd.CommandText = "INSERT INTO message_recipients (message_id, recipient_id) VALUES (@messageId, @recipientId)";
                    insertRecCmd.Parameters.AddWithValue("@messageId", messageId);
                    insertRecCmd.Parameters.AddWithValue("@recipientId", recipientId);
                    insertRecCmd.ExecuteNonQuery();
                }

                tx.Commit();
                formatter.WriteSuccess($"Message sent (ID: {messageId}).");
            }
            catch (Exception ex)
            {
                formatter.WriteError(ex.Message);
                Environment.Exit(1);
            }
        });

        return cmd;
    }

    private static bool IsActiveAgent(Microsoft.Data.Sqlite.SqliteConnection conn, string agentId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM agents WHERE id = @id AND deregistered_at IS NULL";
        cmd.Parameters.AddWithValue("@id", agentId);
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }
}
