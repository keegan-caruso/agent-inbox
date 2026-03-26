namespace AgentInbox.Models;

public sealed class Agent
{
    public string Id { get; init; } = "";
    public string? DisplayName { get; init; }
    public string RegisteredAt { get; init; } = "";
    public string? DeregisteredAt { get; init; }
}
