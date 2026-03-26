using System.CommandLine;
using AgentInbox.Commands;
using AgentInbox.Formatters;

var dbPathDefault = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".agent-inbox",
    "inbox.db");

var dbPathOption = new Option<string>(
    "--db-path",
    description: "Path to the SQLite database file",
    getDefaultValue: () => dbPathDefault);

var formatOption = new Option<OutputFormat>(
    "--format",
    description: "Output format: plain, json, or ndjson",
    getDefaultValue: () => OutputFormat.Plain);
formatOption.AddAlias("-f");

var rootCommand = new RootCommand("agent-inbox: inter-agent communication on a single machine");
rootCommand.AddGlobalOption(dbPathOption);
rootCommand.AddGlobalOption(formatOption);

rootCommand.AddCommand(RegisterCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(DeregisterCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(AgentsCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(SendCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(ReplyCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(InboxCommand.Build(dbPathOption, formatOption));
rootCommand.AddCommand(ReadCommand.Build(dbPathOption, formatOption));

return await rootCommand.InvokeAsync(args);
