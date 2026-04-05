namespace AgentInbox.Models;

public sealed record GroupInboxSearchResult(
    long MessageId,
    string SenderId,
    string? Subject,
    string Body,
    string CreatedAt,
    float Distance);
