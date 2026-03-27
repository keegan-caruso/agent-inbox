namespace AgentInbox.Models;

public sealed class Message
{
    public long Id { get; init; }
    public required string SenderId { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
    public long? ReplyToId { get; init; }
    public required string CreatedAt { get; init; }
}
