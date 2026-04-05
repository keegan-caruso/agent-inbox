using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Commands;

public static class GroupSearchCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var queryArg = new Argument<string>("query") { Description = "Search query for finding similar groups" };
        var limitOption = new Option<int>("--limit") { DefaultValueFactory = _ => 10, Description = "Maximum number of results to return" };

        var cmd = new Command(CommandNames.GroupSearch, CommandNames.Descriptions.GroupSearch) { queryArg, limitOption };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var query = parseResult.GetValue(queryArg)!;
            var limit = parseResult.GetValue(limitOption);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                // Generate embedding for the search query
                var queryEmbedding = EmbeddingGenerator.GenerateEmbedding(query);
                var queryEmbeddingStr = EmbeddingGenerator.FormatEmbedding(queryEmbedding);

                // Perform vector similarity search
                using var searchCmd = conn.CreateCommand();
                searchCmd.CommandText = """
                    SELECT
                        ge.group_id,
                        g.created_at,
                        distance
                    FROM group_embeddings ge
                    JOIN groups g ON g.id = ge.group_id
                    WHERE ge.embedding MATCH @query
                        AND k = @limit
                        AND g.deleted_at IS NULL
                    ORDER BY distance
                    """;
                searchCmd.Parameters.AddWithValue("@query", queryEmbeddingStr);
                searchCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = searchCmd.ExecuteReader();

                var results = new List<GroupSearchResult>();
                while (reader.Read())
                {
                    results.Add(new GroupSearchResult(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetFloat(2)));
                }

                formatter.WriteGroupSearchResults(results);
                return 0;
            }
            catch (SqliteException ex)
            {
                // If vec extension is not available, provide a helpful error
                if (ex.Message.Contains("vec0") || ex.Message.Contains("no such table: group_embeddings"))
                {
                    return CommandExecution.Fail(formatter, "Semantic search is not available. The sqlite-vec extension could not be loaded.");
                }
                return CommandExecution.Fail(formatter, ex);
            }
            catch (Exception ex)
            {
                return CommandExecution.Fail(formatter, ex);
            }
        });

        return cmd;
    }
}
