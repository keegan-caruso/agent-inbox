namespace AgentInbox.Models;

public sealed class MessageRecipient
{
    public long MessageId { get; init; }
    public required string RecipientId { get; init; }
    public bool IsRead { get; init; }
}
