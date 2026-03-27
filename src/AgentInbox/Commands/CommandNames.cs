namespace AgentInbox.Commands;

internal static class CommandNames
{
    // Subcommand names
    public const string Register = "register";
    public const string Deregister = "deregister";
    public const string Agents = "agents";
    public const string Send = "send";
    public const string Reply = "reply";
    public const string Inbox = "inbox";
    public const string Read = "read";

    // Global options
    public const string DbPath = "--db-path";
    public const string Format = "--format";
    public const string FormatAlias = "-f";

    // Shared arguments
    public const string AgentIdArg = "agent-id";
    public const string MessageIdArg = "message-id";

    // register options
    public const string DisplayName = "--display-name";

    // send / reply options
    public const string From = "--from";
    public const string To = "--to";
    public const string Subject = "--subject";
    public const string Body = "--body";
    public const string ToMessage = "--to-message";

    // inbox options
    public const string UnreadOnly = "--unread-only";

    // read options
    public const string As = "--as";
}
