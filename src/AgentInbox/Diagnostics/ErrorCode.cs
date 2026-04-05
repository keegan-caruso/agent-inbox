namespace AgentInbox.Diagnostics;

/// <summary>
/// Standardized error codes for machine-readable failure reporting.
/// </summary>
public static class ErrorCode
{
    // General errors (1xxx)
    public const string UnexpectedError = "E1000";

    // Authentication/authorization errors (2xxx)
    public const string CapabilityTokenRequired = "E2001";
    public const string InvalidCapabilityToken = "E2002";

    // Agent-related errors (3xxx)
    public const string AgentNotFound = "E3001";
    public const string AgentAlreadyRegistered = "E3002";
    public const string AgentAlreadyDeregistered = "E3003";
    public const string AgentNotActive = "E3004";
    public const string AgentIdReservedPrefix = "E3005";

    // Group-related errors (4xxx)
    public const string GroupNotFound = "E4001";
    public const string GroupAlreadyExists = "E4002";
    public const string GroupHasNoActiveMembers = "E4003";
    public const string GroupMemberAlreadyExists = "E4004";
    public const string GroupMemberNotFound = "E4005";

    // Messaging errors (5xxx)
    public const string NoRecipientsSpecified = "E5001";
    public const string RecipientNotActive = "E5002";
    public const string MessageNotFound = "E5003";
    public const string MessageNotAccessible = "E5004";
    public const string NoReplyRecipients = "E5005";
    public const string SenderNotParticipant = "E5006";

    // Search/indexing errors (6xxx)
    public const string SearchQueryRequired = "E6001";
    public const string SearchEmbeddingRequired = "E6002";
    public const string SemanticSearchUnavailable = "E6003";
    public const string InvalidEmbeddingJson = "E6004";
    public const string EmbeddingDimensionMismatch = "E6005";
}
