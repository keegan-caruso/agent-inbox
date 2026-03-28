using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class SendCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };
        var toOpt = new Option<string>(CommandNames.To) { Required = true, Description = CommandNames.Descriptions.SendTo };
        var subjectOpt = new Option<string?>(CommandNames.Subject) { Description = CommandNames.Descriptions.Subject };
        var bodyOpt = new Option<string>(CommandNames.Body) { Required = true, Description = CommandNames.Descriptions.SendBody };

        var cmd = new Command(CommandNames.Send, CommandNames.Descriptions.Send)
        {
            tokenOpt,
            toOpt,
            subjectOpt,
            bodyOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
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
                    return CommandExecution.Fail(formatter, CommandNames.Messages.NoRecipientsSpecified);

                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var senderId))
                    return 1;

                if (!CommandExecution.TryResolveSendRecipients(conn, recipientIds, formatter, out var resolvedRecipientIds))
                    return 1;

                using var tx = conn.BeginTransaction();

                using var insertMsgCmd = conn.CreateCommand();
                insertMsgCmd.Transaction = tx;
                insertMsgCmd.CommandText = "INSERT INTO messages (sender_id, subject, body) VALUES (@senderId, @subject, @body); SELECT last_insert_rowid();";
                insertMsgCmd.Parameters.AddWithValue("@senderId", senderId);
                insertMsgCmd.Parameters.AddWithValue("@subject", (object?)subject ?? DBNull.Value);
                insertMsgCmd.Parameters.AddWithValue("@body", body);
                var messageId = (long)(insertMsgCmd.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert message"));

                using var insertRecCmd = conn.CreateCommand();
                insertRecCmd.Transaction = tx;
                insertRecCmd.CommandText = "INSERT INTO message_recipients (message_id, recipient_id) VALUES (@messageId, @recipientId)";
                insertRecCmd.Parameters.AddWithValue("@messageId", messageId);
                var recipientIdParam = insertRecCmd.CreateParameter();
                recipientIdParam.ParameterName = "@recipientId";
                insertRecCmd.Parameters.Add(recipientIdParam);

                foreach (var recipientId in resolvedRecipientIds)
                {
                    recipientIdParam.Value = recipientId;
                    insertRecCmd.ExecuteNonQuery();
                }

                tx.Commit();
                formatter.WriteSuccess(CommandNames.Messages.MessageSent(messageId));
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
