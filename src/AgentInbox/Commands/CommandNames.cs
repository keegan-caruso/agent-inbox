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

    internal static class Descriptions
    {
        // Root command
        public const string RootCommand = "agent-inbox: inter-agent communication on a single machine";

        // Subcommand descriptions
        public const string Register = "Register an agent";
        public const string Deregister = "Deregister (soft-delete) an agent";
        public const string Agents = "List all active agents";
        public const string Send = "Send a message";
        public const string Reply = "Reply to a message";
        public const string Inbox = "List messages in an agent's inbox";
        public const string Read = "Read a specific message and mark it as read";

        // Global option descriptions
        public const string DbPath = "Path to the SQLite database file";
        public const string Format = "Output format: plain, json, or ndjson";

        // Argument descriptions
        public const string AgentIdArg = "The unique agent identifier";
        public const string MessageIdArg = "Message ID to read";

        // register / deregister option descriptions
        public const string DisplayName = "Optional display name for the agent";

        // send option descriptions
        public const string SendFrom = "Sender agent ID";
        public const string SendTo = "Comma-separated recipient agent IDs";
        public const string Subject = "Message subject";
        public const string SendBody = "Message body";

        // reply option descriptions
        public const string ReplyFrom = "Replying agent ID";
        public const string ToMessage = "Message ID to reply to";
        public const string ReplyBody = "Reply body";

        // inbox option descriptions
        public const string InboxAgentId = "Agent ID to retrieve inbox for";
        public const string UnreadOnly = "Show only unread messages";

        // read option descriptions
        public const string ReadAgentId = "Agent ID reading the message";
    }
}
