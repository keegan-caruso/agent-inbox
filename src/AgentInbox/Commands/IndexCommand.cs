using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class IndexCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var messageIdArg = new Argument<long>(CommandNames.MessageIdArg) { Description = CommandNames.Descriptions.MessageIdArg };
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };
        var embeddingOpt = new Option<string?>(CommandNames.Embedding) { Description = CommandNames.Descriptions.IndexEmbedding };

        var cmd = new Command(CommandNames.Index, CommandNames.Descriptions.Index)
        {
            messageIdArg,
            tokenOpt,
            embeddingOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var messageId = parseResult.GetValue(messageIdArg);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            var embeddingJson = parseResult.GetValue(embeddingOpt);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!ctx.VecLoaded)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.SemanticSearchUnavailable);

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var agentId))
                    return 1;

                // Verify the caller has access to this message (is sender or recipient)
                using var accessCmd = conn.CreateCommand();
                accessCmd.CommandText = """
                    SELECT m.subject, m.body
                    FROM messages m
                    LEFT JOIN message_recipients mr ON mr.message_id = m.id AND mr.recipient_id = @agentId
                    WHERE m.id = @messageId
                      AND (m.sender_id = @agentId OR mr.recipient_id IS NOT NULL)
                    """;
                accessCmd.Parameters.AddWithValue("@messageId", messageId);
                accessCmd.Parameters.AddWithValue("@agentId", agentId);

                string? subject = null;
                string? body = null;
                using (var reader = accessCmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return CommandExecution.Fail(formatter, CommandNames.Messages.MessageNotAccessibleForIndex(messageId));
                    subject = reader.IsDBNull(0) ? null : reader.GetString(0);
                    body = reader.GetString(1);
                }

                float[] embedding;
                if (!string.IsNullOrWhiteSpace(embeddingJson))
                {
                    if (!SearchCommand.TryParseEmbedding(embeddingJson, formatter, out embedding))
                        return 1;
                }
                else
                {
                    var text = string.IsNullOrEmpty(subject) ? body! : $"{subject} {body}";
                    embedding = EmbeddingGenerator.Generate(text);
                }

                if (embedding.Length != EmbeddingGenerator.Dimensions)
                    return CommandExecution.Fail(formatter, CommandNames.Messages.EmbeddingDimensionMismatch(embedding.Length, EmbeddingGenerator.Dimensions));

                var embeddingBytes = SearchCommand.SerializeEmbeddingForVec(embedding);

                using var tx = conn.BeginTransaction();

                // Delete existing embedding if any (vec0 doesn't support UPSERT)
                using var deleteCmd = conn.CreateCommand();
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = "DELETE FROM message_embeddings WHERE message_id = @messageId";
                deleteCmd.Parameters.AddWithValue("@messageId", messageId);
                deleteCmd.ExecuteNonQuery();

                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = "INSERT INTO message_embeddings(message_id, embedding) VALUES (@messageId, @embedding)";
                insertCmd.Parameters.AddWithValue("@messageId", messageId);
                insertCmd.Parameters.AddWithValue("@embedding", embeddingBytes);
                insertCmd.ExecuteNonQuery();

                tx.Commit();

                formatter.WriteSuccess(CommandNames.Messages.EmbeddingStored(messageId));
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
