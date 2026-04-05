using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using Microsoft.Data.Sqlite;

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

                // Extract group IDs before resolving
                var groupIds = new List<string>();
                foreach (var recipient in recipientIds)
                {
                    if (recipient.StartsWith("group:", StringComparison.Ordinal))
                    {
                        var groupId = recipient["group:".Length..];
                        if (!groupIds.Contains(groupId))
                            groupIds.Add(groupId);
                    }
                }

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

                // Store which groups this message was sent to
                if (groupIds.Count > 0)
                {
                    using var insertGroupCmd = conn.CreateCommand();
                    insertGroupCmd.Transaction = tx;
                    insertGroupCmd.CommandText = "INSERT INTO message_groups (message_id, group_id) VALUES (@messageId, @groupId)";
                    insertGroupCmd.Parameters.AddWithValue("@messageId", messageId);
                    var groupIdParam = insertGroupCmd.CreateParameter();
                    groupIdParam.ParameterName = "@groupId";
                    insertGroupCmd.Parameters.Add(groupIdParam);

                    foreach (var groupId in groupIds)
                    {
                        groupIdParam.Value = groupId;
                        insertGroupCmd.ExecuteNonQuery();
                    }
                }

                // Generate and store embedding for the message
                StoreMessageEmbedding(conn, tx, messageId, subject, body);

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

    private static void StoreMessageEmbedding(SqliteConnection conn, SqliteTransaction tx, long messageId, string? subject, string body)
    {
        try
        {
            // Combine subject and body for embedding
            var text = subject != null ? $"{subject} {body}" : body;
            var embedding = EmbeddingGenerator.GenerateEmbedding(text);
            var embeddingStr = EmbeddingGenerator.FormatEmbedding(embedding);

            // Delete existing embedding if any
            using var delCmd = conn.CreateCommand();
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM message_embeddings WHERE message_id = @id";
            delCmd.Parameters.AddWithValue("@id", messageId);
            delCmd.ExecuteNonQuery();

            // Insert new embedding
            using var insCmd = conn.CreateCommand();
            insCmd.Transaction = tx;
            insCmd.CommandText = "INSERT INTO message_embeddings (message_id, embedding) VALUES (@id, @embedding)";
            insCmd.Parameters.AddWithValue("@id", messageId);
            insCmd.Parameters.AddWithValue("@embedding", embeddingStr);
            insCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // If vec extension is not available, skip embedding storage
            // This is not a critical error
        }
    }
}
