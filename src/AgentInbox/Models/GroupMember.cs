namespace AgentInbox.Models;

public sealed class GroupMember
{
    public required string AgentId { get; init; }
    public string? DisplayName { get; init; }
}
