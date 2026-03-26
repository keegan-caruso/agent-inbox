namespace AgentInbox.Formatters;

public interface IOutputFormatter
{
    void WriteAgents(IReadOnlyList<Models.Agent> agents);
    void WriteMessage(Models.Message message);
    void WriteInbox(IReadOnlyList<Models.InboxEntry> entries);
    void WriteSuccess(string message);
    void WriteError(string message);
}
