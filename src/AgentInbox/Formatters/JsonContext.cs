using System.Text.Json.Serialization;
using AgentInbox.Diagnostics;
using AgentInbox.Models;

namespace AgentInbox.Formatters;

[JsonSerializable(typeof(Agent))]
[JsonSerializable(typeof(List<Agent>))]
[JsonSerializable(typeof(GroupMember))]
[JsonSerializable(typeof(List<GroupMember>))]
[JsonSerializable(typeof(Group))]
[JsonSerializable(typeof(List<Group>))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(List<Message>))]
[JsonSerializable(typeof(MessageRecipient))]
[JsonSerializable(typeof(List<MessageRecipient>))]
[JsonSerializable(typeof(InboxEntry))]
[JsonSerializable(typeof(List<InboxEntry>))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<float>))]
[JsonSerializable(typeof(RegistrationResult))]
[JsonSerializable(typeof(GroupMembersResult))]
[JsonSerializable(typeof(SuccessResult))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(ErrorResultWithCode))]
[JsonSerializable(typeof(DiagnosticEvent))]
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

public sealed class ErrorResultWithCode
{
    public string Error { get; init; } = "";
    public string ErrorCode { get; init; } = "";
}

public sealed record GroupMembersResult(string GroupId, List<GroupMember> Members);
