using System.Text.Json.Serialization;
using AgentInbox.Models;

namespace AgentInbox.Formatters;

[JsonSerializable(typeof(Agent))]
[JsonSerializable(typeof(List<Agent>))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(List<Message>))]
[JsonSerializable(typeof(MessageRecipient))]
[JsonSerializable(typeof(List<MessageRecipient>))]
[JsonSerializable(typeof(InboxEntry))]
[JsonSerializable(typeof(List<InboxEntry>))]
[JsonSerializable(typeof(SuccessResult))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class JsonContext : JsonSerializerContext
{
}

public sealed class SuccessResult
{
    public string Message { get; init; } = "";
}

public sealed class ErrorResult
{
    public string Error { get; init; } = "";
}
