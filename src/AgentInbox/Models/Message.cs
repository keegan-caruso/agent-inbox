namespace AgentInbox.Models;

public sealed class Message
{
    public long Id { get; init; }
    public string SenderId { get; init; } = "";
    public string? Subject { get; init; }
    public string Body { get; init; } = "";
    public long? ReplyToId { get; init; }
    public string CreatedAt { get; init; } = "";
}
