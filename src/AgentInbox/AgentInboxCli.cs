using System.CommandLine;
using AgentInbox.Commands;
using AgentInbox.Diagnostics;
using AgentInbox.Formatters;

namespace AgentInbox;

public static class AgentInboxCli
{
    public static RootCommand CreateRootCommand()
    {
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

        var verboseOption = new Option<bool>(CommandNames.Verbose)
        {
            Recursive = true,
            DefaultValueFactory = _ => false,
            Description = CommandNames.Descriptions.Verbose
        };
        verboseOption.Aliases.Add(CommandNames.VerboseAlias);

        var rootCommand = new RootCommand(CommandNames.Descriptions.RootCommand);
        rootCommand.Add(dbPathOption);
        rootCommand.Add(formatOption);
        rootCommand.Add(verboseOption);

        rootCommand.Add(RegisterCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(DeregisterCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(AgentsCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupCreateCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupDeleteCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupsCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupAddMemberCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupRemoveMemberCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(GroupMembersCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(SendCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(ReplyCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(InboxCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(ReadCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(SearchCommand.Build(dbPathOption, formatOption));
        rootCommand.Add(IndexCommand.Build(dbPathOption, formatOption));

        return rootCommand;
    }

    public static async Task<int> InvokeAsync(string[] args)
    {
        // Simple verbose check - look for the flag in args before parsing
        var verboseEnabled = args.Contains("--verbose") || args.Contains("-v");
        if (verboseEnabled)
            DiagnosticManager.SetVerboseMode(true);

        var rootCommand = CreateRootCommand();
        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
