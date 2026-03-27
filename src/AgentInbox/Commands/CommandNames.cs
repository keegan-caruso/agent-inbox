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

    internal static class Messages
    {
        // register
        public static string AgentRegistered(string id) => $"Agent '{id}' registered successfully.";
        public static string AgentAlreadyRegistered(string id) => $"Agent '{id}' is already registered.";
        public static string AgentReactivated(string id) => $"Agent '{id}' reactivated.";

        // deregister
        public static string AgentNotFound(string id) => $"Agent '{id}' not found.";
        public static string AgentAlreadyDeregistered(string id) => $"Agent '{id}' is already deregistered.";
        public static string AgentDeregistered(string id) => $"Agent '{id}' deregistered.";

        // send
        public const string NoRecipientsSpecified = "No recipients specified.";
        public static string AgentNotActive(string id) => $"Agent '{id}' is not an active registered agent.";
        public static string SenderNotActive(string id) => $"Sender '{id}' is not an active registered agent.";
        public static string RecipientNotActive(string id) => $"Recipient '{id}' is not an active registered agent.";
        public static string MessageSent(long id) => $"Message sent (ID: {id}).";

        // reply
        public static string MessageNotFound(long id) => $"Message {id} not found.";
        public static string MessageNotAccessible(long id, string agentId) => $"Message {id} not found for agent '{agentId}'.";
        public const string NoReplyRecipients = "No recipients for the reply (you are the only participant).";
        public static string SenderNotParticipant(string id, long messageId) => $"Sender '{id}' is not a participant in message {messageId} and cannot reply to it.";
        public static string ReplySent(long id) => $"Reply sent (ID: {id}).";
    }

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
