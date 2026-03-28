namespace AgentInbox.Formatters;

public sealed class RegistrationResult
{
    public string Message { get; init; } = "";
    public string AgentId { get; init; } = "";
    public string CapabilityToken { get; init; } = "";
}
