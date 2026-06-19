using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;

namespace AgentInbox.Commands;

public static class RememberCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var tokenOpt = new Option<string?>(CommandNames.Token) { Description = CommandNames.Descriptions.CapabilityToken };
        var contentOpt = new Option<string>(CommandNames.Content) { Required = true, Description = CommandNames.Descriptions.MemoryContent };
        var tagsOpt = new Option<string?>(CommandNames.Tags) { Description = CommandNames.Descriptions.MemoryTags };

        var cmd = new Command(CommandNames.Remember, CommandNames.Descriptions.Remember)
        {
            tokenOpt,
            contentOpt,
            tagsOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var content = parseResult.GetValue(contentOpt)!;
            var tags = parseResult.GetValue(tagsOpt);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                if (!CommandExecution.TryResolveActiveAgentId(conn, parseResult, tokenOpt, formatter, out var agentId))
                    return 1;

                using var tx = conn.BeginTransaction();

                using var insertMsgCmd = conn.CreateCommand();
                insertMsgCmd.Transaction = tx;
                insertMsgCmd.CommandText = "INSERT INTO messages (sender_id, subject, body) VALUES (@senderId, @subject, @body); SELECT last_insert_rowid();";
                insertMsgCmd.Parameters.AddWithValue("@senderId", agentId);
                insertMsgCmd.Parameters.AddWithValue("@subject", string.IsNullOrWhiteSpace(tags) ? DBNull.Value : (object)tags.Trim());
                insertMsgCmd.Parameters.AddWithValue("@body", content);
                var messageId = (long)(insertMsgCmd.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert memory"));

                using var insertRecCmd = conn.CreateCommand();
                insertRecCmd.Transaction = tx;
                insertRecCmd.CommandText = "INSERT INTO message_recipients (message_id, recipient_id) VALUES (@messageId, @agentId)";
                insertRecCmd.Parameters.AddWithValue("@messageId", messageId);
                insertRecCmd.Parameters.AddWithValue("@agentId", agentId);
                insertRecCmd.ExecuteNonQuery();

                tx.Commit();
                formatter.WriteSuccess(CommandNames.Messages.MemoryStored(messageId));
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
