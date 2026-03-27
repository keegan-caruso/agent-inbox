using System.Text.Json;
using AgentInbox.Models;

namespace AgentInbox.Formatters;

public sealed class NdjsonFormatter : IOutputFormatter
{
    public void WriteAgents(IReadOnlyList<Agent> agents)
    {
        foreach (var agent in agents)
            Console.WriteLine(JsonSerializer.Serialize(agent, JsonContext.Default.Agent));
    }

    public void WriteMessage(Message message) =>
        Console.WriteLine(JsonSerializer.Serialize(message, JsonContext.Default.Message));

    public void WriteInbox(IReadOnlyList<InboxEntry> entries)
    {
        foreach (var entry in entries)
            Console.WriteLine(JsonSerializer.Serialize(entry, JsonContext.Default.InboxEntry));
    }

    public void WriteSuccess(string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new SuccessResult { Message = message }, JsonContext.Default.SuccessResult));

    public void WriteError(string message) =>
        Console.Error.WriteLine(JsonSerializer.Serialize(new ErrorResult { Error = message }, JsonContext.Default.ErrorResult));
}
