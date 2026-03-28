namespace AgentInbox.Commands;

internal static class CommandNames
{
    // Subcommand names
    public const string Register = "register";
    public const string Deregister = "deregister";
    public const string Agents = "agents";
    public const string GroupCreate = "group-create";
    public const string GroupDelete = "group-delete";
    public const string Groups = "groups";
    public const string GroupAddMember = "group-add-member";
    public const string GroupRemoveMember = "group-remove-member";
    public const string GroupMembers = "group-members";
    public const string Send = "send";
    public const string Reply = "reply";
    public const string Inbox = "inbox";
    public const string Read = "read";

    // Global options
    public const string DbPath = "--db-path";
    public const string Format = "--format";
    public const string FormatAlias = "-f";
    public const string Token = "--token";
    public const string CapabilityTokenEnvVar = "AGENT_INBOX_CAPABILITY_TOKEN";

    // Shared arguments
    public const string AgentIdArg = "agent-id";
    public const string GroupIdArg = "group-id";
    public const string MessageIdArg = "message-id";

    // register options
    public const string DisplayName = "--display-name";

    // messaging options
    public const string To = "--to";
    public const string Subject = "--subject";
    public const string Body = "--body";
    public const string ToMessage = "--to-message";

    // inbox options
    public const string UnreadOnly = "--unread-only";

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

        // groups
        public static string GroupCreated(string id) => $"Group '{id}' created successfully.";
        public static string GroupAlreadyExists(string id) => $"Group '{id}' already exists.";
        public static string GroupNotFound(string id) => $"Group '{id}' not found.";
        public static string GroupDeleted(string id) => $"Group '{id}' deleted.";
        public static string GroupMemberAdded(string groupId, string agentId) =>
            $"Agent '{agentId}' added to group '{groupId}'.";
        public static string GroupMemberAlreadyExists(string groupId, string agentId) =>
            $"Agent '{agentId}' is already a member of group '{groupId}'.";
        public static string GroupMemberNotFound(string groupId, string agentId) =>
            $"Agent '{agentId}' is not a member of group '{groupId}'.";
        public static string GroupMemberRemoved(string groupId, string agentId) =>
            $"Agent '{agentId}' removed from group '{groupId}'.";

        // send
        public const string NoRecipientsSpecified = "No recipients specified.";
        public static string AgentNotActive(string id) => $"Agent '{id}' is not an active registered agent.";
        public static string SenderNotActive(string id) => $"Sender '{id}' is not an active registered agent.";
        public static string RecipientNotActive(string id) => $"Recipient '{id}' is not an active registered agent.";
        public static string GroupHasNoActiveMembers(string id) => $"Group '{id}' has no active members.";
        public static string MessageSent(long id) => $"Message sent (ID: {id}).";
        public const string CapabilityTokenRequired = "Capability token is required.";
        public const string InvalidCapabilityToken = "Invalid token.";

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
        public const string GroupCreate = "Create a group";
        public const string GroupDelete = "Delete a group";
        public const string Groups = "List all groups";
        public const string GroupAddMember = "Add an agent to a group";
        public const string GroupRemoveMember = "Remove an agent from a group";
        public const string GroupMembers = "List members of a group";
        public const string Send = "Send a message using a capability token";
        public const string Reply = "Reply to a message using a capability token";
        public const string Inbox = "List messages for the inbox authorized by a capability token";
        public const string Read = "Read a specific message and mark it as read using a capability token";

        // Global option descriptions
        public const string DbPath = "Path to the SQLite database file";
        public const string Format = "Output format: plain, json, or ndjson";
        public const string CapabilityToken = $"Capability token (or {CapabilityTokenEnvVar})";

        // Argument descriptions
        public const string AgentIdArg = "The unique agent identifier";
        public const string GroupIdArg = "The unique group identifier";
        public const string MessageIdArg = "Message ID to read";

        // register / deregister option descriptions
        public const string DisplayName = "Optional display name for the agent";

        // send option descriptions
        public const string SendTo = "Comma-separated recipient IDs (agent-id or group:<group-id>)";
        public const string Subject = "Message subject";
        public const string SendBody = "Message body";

        // reply option descriptions
        public const string ToMessage = "Message ID to reply to";
        public const string ReplyBody = "Reply body";

        // inbox option descriptions
        public const string UnreadOnly = "Show only unread messages";
    }
}
