namespace AgentInbox.Models;

public sealed class Agent
{
    public required string Id { get; init; }
    public string? DisplayName { get; init; }
    public required string RegisteredAt { get; init; }
    public string? DeregisteredAt { get; init; }
}
