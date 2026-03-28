using System.CommandLine;
using AgentInbox.Database;
using AgentInbox.Formatters;
using AgentInbox.Security;

namespace AgentInbox.Commands;

public static class RegisterCommand
{
    public static Command Build(Option<string> dbPathOption, Option<OutputFormat> formatOption)
    {
        var agentIdArg = new Argument<string>(CommandNames.AgentIdArg) { Description = CommandNames.Descriptions.AgentIdArg };
        var displayNameOpt = new Option<string?>(CommandNames.DisplayName) { Description = CommandNames.Descriptions.DisplayName };

        var cmd = new Command(CommandNames.Register, CommandNames.Descriptions.Register)
        {
            agentIdArg,
            displayNameOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var agentId = parseResult.GetValue(agentIdArg)!;
            var displayName = parseResult.GetValue(displayNameOpt);
            var dbPath = parseResult.GetValue(dbPathOption)!;
            var format = parseResult.GetValue(formatOption);
            var formatter = FormatterFactory.Create(format);
            try
            {
                using var ctx = new DbContext(dbPath);
                var conn = ctx.Connection;

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT id, deregistered_at FROM agents WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", agentId);
                using var reader = checkCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    var capabilityToken = CapabilityTokens.Generate();
                    var capabilityTokenHash = CapabilityTokens.Hash(capabilityToken);
                    using var insertCmd = conn.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO agents (id, display_name, capability_token_hash) VALUES (@id, @displayName, @capabilityTokenHash)";
                    insertCmd.Parameters.AddWithValue("@id", agentId);
                    insertCmd.Parameters.AddWithValue("@displayName", (object?)displayName ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@capabilityTokenHash", capabilityTokenHash);
                    insertCmd.ExecuteNonQuery();
                    formatter.WriteRegistration(new RegistrationResult
                    {
                        Message = CommandNames.Messages.AgentRegistered(agentId),
                        AgentId = agentId,
                        CapabilityToken = capabilityToken
                    });
                }
                else
                {
                    bool isDeregistered = !reader.IsDBNull(1);
                    reader.Close();

                    if (!isDeregistered)
                        return CommandExecution.Fail(formatter, CommandNames.Messages.AgentAlreadyRegistered(agentId));

                    var capabilityToken = CapabilityTokens.Generate();
                    var capabilityTokenHash = CapabilityTokens.Hash(capabilityToken);
                    using var reactivateCmd = conn.CreateCommand();
                    reactivateCmd.Parameters.AddWithValue("@id", agentId);
                    reactivateCmd.Parameters.AddWithValue("@capabilityTokenHash", capabilityTokenHash);
                    if (displayName is null)
                    {
                        reactivateCmd.CommandText = """
                            UPDATE agents
                            SET deregistered_at = NULL,
                                capability_token_hash = @capabilityTokenHash,
                                capability_token_created_at = datetime('now'),
                                registered_at = datetime('now')
                            WHERE id = @id
                            """;
                    }
                    else
                    {
                        reactivateCmd.CommandText = """
                            UPDATE agents
                            SET deregistered_at = NULL,
                                display_name = @displayName,
                                capability_token_hash = @capabilityTokenHash,
                                capability_token_created_at = datetime('now'),
                                registered_at = datetime('now')
                            WHERE id = @id
                            """;
                        reactivateCmd.Parameters.AddWithValue("@displayName", displayName);
                    }

                    reactivateCmd.ExecuteNonQuery();
                    formatter.WriteRegistration(new RegistrationResult
                    {
                        Message = CommandNames.Messages.AgentReactivated(agentId),
                        AgentId = agentId,
                        CapabilityToken = capabilityToken
                    });
                }

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
