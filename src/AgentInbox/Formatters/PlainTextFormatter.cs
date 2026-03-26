using AgentInbox.Models;

namespace AgentInbox.Formatters;

public sealed class PlainTextFormatter : IOutputFormatter
{
    public void WriteAgents(IReadOnlyList<Agent> agents)
    {
        if (agents.Count == 0)
        {
            Console.WriteLine("No active agents registered.");
            return;
        }
        Console.WriteLine($"{"ID",-30} {"Display Name",-30} {"Registered At",-20}");
        Console.WriteLine(new string('-', 82));
        foreach (var agent in agents)
        {
            Console.WriteLine($"{agent.Id,-30} {agent.DisplayName ?? "",-30} {agent.RegisteredAt,-20}");
        }
    }

    public void WriteMessage(Message message)
    {
        Console.WriteLine($"ID:         {message.Id}");
        Console.WriteLine($"From:       {message.SenderId}");
        if (message.Subject != null)
            Console.WriteLine($"Subject:    {message.Subject}");
        if (message.ReplyToId.HasValue)
            Console.WriteLine($"Reply-To:   {message.ReplyToId}");
        Console.WriteLine($"Created At: {message.CreatedAt}");
        Console.WriteLine();
        Console.WriteLine(message.Body);
    }

    public void WriteInbox(IReadOnlyList<InboxEntry> entries)
    {
        if (entries.Count == 0)
        {
            Console.WriteLine("No messages.");
            return;
        }
        Console.WriteLine($"{"ID",-6} {"Read",-5} {"From",-20} {"Subject",-30} {"Date",-20}");
        Console.WriteLine(new string('-', 83));
        foreach (var entry in entries)
        {
            string read = entry.IsRead ? "Y" : "N";
            string subject = entry.Subject ?? "(no subject)";
            Console.WriteLine($"{entry.MessageId,-6} {read,-5} {entry.SenderId,-20} {subject,-30} {entry.CreatedAt,-20}");
        }
    }

    public void WriteSuccess(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteError(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
    }
}
