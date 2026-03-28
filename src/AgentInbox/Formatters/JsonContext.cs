using System.Text.Json.Serialization;
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
[JsonSerializable(typeof(RegistrationResult))]
[JsonSerializable(typeof(GroupMembersResult))]
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

public sealed class GroupMembersResult
{
    public string GroupId { get; init; } = "";
    public List<GroupMember> Members { get; init; } = [];
}
