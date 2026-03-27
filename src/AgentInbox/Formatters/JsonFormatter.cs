using System.Text.Json;
using AgentInbox.Models;

namespace AgentInbox.Formatters;

public sealed class JsonFormatter : IOutputFormatter
{
    public void WriteAgents(IReadOnlyList<Agent> agents) =>
        Console.WriteLine(JsonSerializer.Serialize(new List<Agent>(agents), JsonContext.Default.ListAgent));

    public void WriteMessage(Message message) =>
        Console.WriteLine(JsonSerializer.Serialize(message, JsonContext.Default.Message));

    public void WriteInbox(IReadOnlyList<InboxEntry> entries) =>
        Console.WriteLine(JsonSerializer.Serialize(new List<InboxEntry>(entries), JsonContext.Default.ListInboxEntry));

    public void WriteSuccess(string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new SuccessResult { Message = message }, JsonContext.Default.SuccessResult));

    public void WriteError(string message) =>
        Console.Error.WriteLine(JsonSerializer.Serialize(new ErrorResult { Error = message }, JsonContext.Default.ErrorResult));
}
