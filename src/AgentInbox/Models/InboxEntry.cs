namespace AgentInbox.Models;

public sealed class InboxEntry
{
    public long MessageId { get; init; }
    public string SenderId { get; init; } = "";
    public string? Subject { get; init; }
    public string Body { get; init; } = "";
    public long? ReplyToId { get; init; }
    public string CreatedAt { get; init; } = "";
    public bool IsRead { get; init; }
}
