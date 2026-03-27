using System.CommandLine;
using AgentInbox.Commands;
using AgentInbox.Formatters;

var dbPathDefault = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".agent-inbox",
    "inbox.db");

var dbPathOption = new Option<string>(CommandNames.DbPath)
{
    Recursive = true,
    DefaultValueFactory = _ => dbPathDefault,
    Description = CommandNames.Descriptions.DbPath
};

var formatOption = new Option<OutputFormat>(CommandNames.Format)
{
    Recursive = true,
    DefaultValueFactory = _ => OutputFormat.Plain,
    Description = CommandNames.Descriptions.Format
};
formatOption.Aliases.Add(CommandNames.FormatAlias);

var rootCommand = new RootCommand(CommandNames.Descriptions.RootCommand);
rootCommand.Add(dbPathOption);
rootCommand.Add(formatOption);

rootCommand.Add(RegisterCommand.Build(dbPathOption, formatOption));
rootCommand.Add(DeregisterCommand.Build(dbPathOption, formatOption));
rootCommand.Add(AgentsCommand.Build(dbPathOption, formatOption));
rootCommand.Add(SendCommand.Build(dbPathOption, formatOption));
rootCommand.Add(ReplyCommand.Build(dbPathOption, formatOption));
rootCommand.Add(InboxCommand.Build(dbPathOption, formatOption));
rootCommand.Add(ReadCommand.Build(dbPathOption, formatOption));

return await rootCommand.Parse(args).InvokeAsync();
