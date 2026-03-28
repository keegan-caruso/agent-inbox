using System.Text.Json;
using AgentInbox.Models;

namespace AgentInbox.Formatters;

public sealed class JsonFormatter : IOutputFormatter
{
    public void WriteAgents(IReadOnlyList<Agent> agents) =>
        Console.WriteLine(JsonSerializer.Serialize(new List<Agent>(agents), JsonContext.Default.ListAgent));

    public void WriteGroups(IReadOnlyList<Group> groups) =>
        Console.WriteLine(JsonSerializer.Serialize(new List<Group>(groups), JsonContext.Default.ListGroup));

    public void WriteGroupMembers(string groupId, IReadOnlyList<GroupMember> members) =>
        Console.WriteLine(JsonSerializer.Serialize(new GroupMembersResult
        {
            GroupId = groupId,
            Members = new List<GroupMember>(members)
        }, JsonContext.Default.GroupMembersResult));

    public void WriteMessage(Message message) =>
        Console.WriteLine(JsonSerializer.Serialize(message, JsonContext.Default.Message));

    public void WriteInbox(IReadOnlyList<InboxEntry> entries) =>
        Console.WriteLine(JsonSerializer.Serialize(new List<InboxEntry>(entries), JsonContext.Default.ListInboxEntry));

    public void WriteRegistration(RegistrationResult result) =>
        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.RegistrationResult));

    public void WriteSuccess(string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new SuccessResult { Message = message }, JsonContext.Default.SuccessResult));

    public void WriteError(string message) =>
        Console.Error.WriteLine(JsonSerializer.Serialize(new ErrorResult { Error = message }, JsonContext.Default.ErrorResult));
}
