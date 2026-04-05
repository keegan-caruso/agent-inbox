using System.CommandLine;
using System.Text.Json;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;

namespace AgentInbox.Commands;

public static class SearchCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };
        var queryOpt = new Option<string?>(CommandNames.Query) { Description = CommandNames.Descriptions.SearchQuery };
        var modeOpt = new Option<string>(CommandNames.Mode)
        {
            Description = CommandNames.Descriptions.SearchMode,
            DefaultValueFactory = _ => "text"
        };
        var embeddingOpt = new Option<string?>(CommandNames.Embedding) { Description = CommandNames.Descriptions.SearchEmbedding };
        var limitOpt = new Option<int>(CommandNames.Limit)
        {
            Description = CommandNames.Descriptions.SearchLimit,
            DefaultValueFactory = _ => 10
        };

        var cmd = new Command(CommandNames.Search, CommandNames.Descriptions.Search)
        {
            tokenOpt,
            queryOpt,
            modeOpt,
            embeddingOpt,
            limitOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            var query = parseResult.GetValue(queryOpt);
            var mode = parseResult.GetValue(modeOpt) ?? "text";
            var embeddingJson = parseResult.GetValue(embeddingOpt);
            var limit = parseResult.GetValue(limitOpt);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var agentId))
                    return 1;

                if (mode == "semantic")
                    return RunSemanticSearch(ctx, agentId, query, embeddingJson, limit, formatter);

                // Default: text mode (FTS5)
                if (string.IsNullOrWhiteSpace(query))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.SearchQueryRequired);

                return RunTextSearch(ctx, agentId, query, limit, formatter);
            }
            catch (Exception ex)
            {
                return CommandExecution.Fail(formatter, ex);
            }
        });

        return cmd;
    }

    private static int RunTextSearch(DbContext ctx, string agentId, string query, int limit, IOutputFormatter formatter)
    {
        var conn = ctx.Connection;

        using var searchCmd = conn.CreateCommand();
        searchCmd.CommandText = """
            SELECT m.id, m.sender_id, m.subject, m.body, m.reply_to_id, m.created_at, mr.is_read,
                   rank
            FROM messages_fts fts
            JOIN messages m ON m.id = fts.rowid
            JOIN message_recipients mr ON mr.message_id = m.id
            WHERE messages_fts MATCH @query
              AND mr.recipient_id = @agentId
            ORDER BY rank
            LIMIT @limit
            """;
        searchCmd.Parameters.AddWithValue("@query", query);
        searchCmd.Parameters.AddWithValue("@agentId", agentId);
        searchCmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<SearchResult>();
        using var reader = searchCmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchResult
            {
                MessageId = reader.GetInt64(0),
                SenderId = reader.GetString(1),
                Subject = reader.IsDBNull(2) ? null : reader.GetString(2),
                Body = reader.GetString(3),
                ReplyToId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                CreatedAt = reader.GetString(5),
                IsRead = reader.GetInt32(6) != 0,
                Score = reader.GetDouble(7)
            });
        }

        formatter.WriteSearchResults(results);
        return 0;
    }

    private static int RunSemanticSearch(DbContext ctx, string agentId, string? query, string? embeddingJson, int limit, IOutputFormatter formatter)
    {
        if (!ctx.VecLoaded)
            return CommandExecution.Fail(formatter, CommandNames.Messages.SemanticSearchUnavailable);

        float[] queryEmbedding;
        if (!string.IsNullOrWhiteSpace(embeddingJson))
        {
            if (!TryParseEmbedding(embeddingJson, formatter, out queryEmbedding))
                return 1;
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            queryEmbedding = EmbeddingGenerator.Generate(query);
        }
        else
        {
            return CommandExecution.Fail(formatter, CommandNames.Messages.SearchEmbeddingRequired);
        }

        if (queryEmbedding.Length != EmbeddingGenerator.Dimensions)
            return CommandExecution.Fail(formatter, CommandNames.Messages.EmbeddingDimensionMismatch(queryEmbedding.Length, EmbeddingGenerator.Dimensions));

        var conn = ctx.Connection;

        using var searchCmd = conn.CreateCommand();
        searchCmd.CommandText = """
            SELECT m.id, m.sender_id, m.subject, m.body, m.reply_to_id, m.created_at, mr.is_read,
                   vec.distance
            FROM message_embeddings vec
            JOIN messages m ON m.id = vec.message_id
            JOIN message_recipients mr ON mr.message_id = m.id
            WHERE embedding MATCH @queryEmbedding
              AND k = @limit
              AND mr.recipient_id = @agentId
            ORDER BY vec.distance
            """;
        searchCmd.Parameters.AddWithValue("@queryEmbedding", SerializeEmbeddingForVec(queryEmbedding));
        searchCmd.Parameters.AddWithValue("@limit", limit);
        searchCmd.Parameters.AddWithValue("@agentId", agentId);

        var results = new List<SearchResult>();
        using var reader = searchCmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchResult
            {
                MessageId = reader.GetInt64(0),
                SenderId = reader.GetString(1),
                Subject = reader.IsDBNull(2) ? null : reader.GetString(2),
                Body = reader.GetString(3),
                ReplyToId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                CreatedAt = reader.GetString(5),
                IsRead = reader.GetInt32(6) != 0,
                Score = reader.GetDouble(7)
            });
        }

        formatter.WriteSearchResults(results);
        return 0;
    }

    internal static bool TryParseEmbedding(string json, IOutputFormatter formatter, out float[] embedding)
    {
        embedding = [];
        try
        {
            var floats = JsonSerializer.Deserialize(json, JsonContext.Default.ListSingle);
            if (floats == null)
            {
                formatter.WriteError(CommandNames.Messages.InvalidEmbeddingJson);
                return false;
            }
            embedding = [.. floats];
            return true;
        }
        catch
        {
            formatter.WriteError(CommandNames.Messages.InvalidEmbeddingJson);
            return false;
        }
    }

    /// <summary>Serializes float[] to the binary BLOB format expected by sqlite-vec (little-endian IEEE 754).</summary>
    internal static byte[] SerializeEmbeddingForVec(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        for (var i = 0; i < embedding.Length; i++)
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(float)), embedding[i]);
        return bytes;
    }
}
