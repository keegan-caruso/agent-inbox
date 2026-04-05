using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Models;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Commands;

public static class GroupInboxSearchCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var groupIdArg = new Argument<string>(CommandNames.GroupIdArg) { Description = "The group ID to search messages for" };
        var queryArg = new Argument<string>("query") { Description = "Search query for finding similar messages" };
        var limitOption = new Option<int>("--limit") { DefaultValueFactory = _ => 10, Description = "Maximum number of results to return" };

        var cmd = new Command(CommandNames.GroupInboxSearch, CommandNames.Descriptions.GroupInboxSearch) { groupIdArg, queryArg, limitOption };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var groupId = parseResult.GetValue(groupIdArg)!;
            var query = parseResult.GetValue(queryArg)!;
            var limit = parseResult.GetValue(limitOption);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);

            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                // Verify group exists
                if (!CommandExecution.GroupExists(conn, groupId))
                    return CommandExecution.Fail(formatter, CommandNames.Messages.GroupNotFound(groupId));

                // Generate embedding for the search query
                var queryEmbedding = EmbeddingGenerator.GenerateEmbedding(query);
                var queryEmbeddingStr = EmbeddingGenerator.FormatEmbedding(queryEmbedding);

                // Perform vector similarity search over messages sent to this group
                using var searchCmd = conn.CreateCommand();
                searchCmd.CommandText = """
                    SELECT
                        m.id,
                        m.sender_id,
                        m.subject,
                        m.body,
                        m.created_at,
                        distance
                    FROM message_embeddings me
                    JOIN messages m ON m.id = me.message_id
                    JOIN message_groups mg ON mg.message_id = m.id
                    WHERE me.embedding MATCH @query
                        AND k = @limit
                        AND mg.group_id = @groupId
                    ORDER BY distance
                    """;
                searchCmd.Parameters.AddWithValue("@query", queryEmbeddingStr);
                searchCmd.Parameters.AddWithValue("@limit", limit);
                searchCmd.Parameters.AddWithValue("@groupId", groupId);

                using var reader = searchCmd.ExecuteReader();

                var results = new List<GroupInboxSearchResult>();
                while (reader.Read())
                {
                    results.Add(new GroupInboxSearchResult(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetFloat(5)));
                }

                formatter.WriteGroupInboxSearchResults(results);
                return 0;
            }
            catch (SqliteException ex)
            {
                // If vec extension is not available, provide a helpful error
                if (ex.Message.Contains("vec0") || ex.Message.Contains("no such table: message_embeddings"))
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
