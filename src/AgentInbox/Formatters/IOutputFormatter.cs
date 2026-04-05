namespace AgentInbox.Formatters;

public interface IOutputFormatter
{
    void WriteAgents(IReadOnlyList<Models.Agent> agents);
    void WriteGroups(IReadOnlyList<Models.Group> groups);
    void WriteGroupMembers(string groupId, IReadOnlyList<Models.GroupMember> members);
    void WriteMessage(Models.Message message);
    void WriteInbox(IReadOnlyList<Models.InboxEntry> entries);
    void WriteSearchResults(IReadOnlyList<Models.SearchResult> results);
    void WriteRegistration(RegistrationResult result);
    void WriteSuccess(string message);
    void WriteError(string message);
}
