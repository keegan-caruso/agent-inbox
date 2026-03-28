namespace AgentInbox.Formatters;

public sealed class RegistrationResult
{
    public required string Message { get; init; }
    public required string AgentId { get; init; }
    public required string CapabilityToken { get; init; }
}
