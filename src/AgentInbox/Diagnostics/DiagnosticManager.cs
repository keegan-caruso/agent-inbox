using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace AgentInbox.Diagnostics;

/// <summary>
/// Manages diagnostic event emission, activity tracing, and metrics.
/// </summary>
public static class DiagnosticManager
{
    private static bool _verboseEnabled;
    private static readonly ActivitySource ActivitySource = new("AgentInbox", "1.0.0");
    private static readonly Meter Meter = new("AgentInbox", "1.0.0");

    // Metrics
    private static readonly Counter<long> CommandExecutionCounter = Meter.CreateCounter<long>(
        "agent_inbox.command.executions",
        "count",
        "Number of command executions");

    private static readonly Counter<long> CommandErrorCounter = Meter.CreateCounter<long>(
        "agent_inbox.command.errors",
        "count",
        "Number of command errors");

    private static readonly Histogram<double> CommandDurationHistogram = Meter.CreateHistogram<double>(
        "agent_inbox.command.duration",
        "ms",
        "Command execution duration");

    private static readonly Counter<long> SearchExecutionCounter = Meter.CreateCounter<long>(
        "agent_inbox.search.executions",
        "count",
        "Number of search executions");

    private static readonly Counter<long> IndexExecutionCounter = Meter.CreateCounter<long>(
        "agent_inbox.index.executions",
        "count",
        "Number of index executions");

    public static void SetVerboseMode(bool enabled)
    {
        _verboseEnabled = enabled;
    }

    public static bool IsVerboseEnabled => _verboseEnabled;

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    public static void EmitEvent(DiagnosticEvent evt)
    {
        if (!_verboseEnabled)
            return;

        var json = JsonSerializer.Serialize(evt, Formatters.JsonContext.Default.DiagnosticEvent);
        Console.Error.WriteLine(json);
    }

    public static void EmitCommandStart(string commandName, string? dbPath = null, string? dbPathSource = null)
    {
        CommandExecutionCounter.Add(1, new KeyValuePair<string, object?>("command", commandName));

        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.CommandStart,
            CommandName = commandName,
            ActivityId = Activity.Current?.Id,
            DbPath = dbPath,
            DbPathSource = dbPathSource
        });
    }

    public static void EmitCommandEnd(string commandName, double durationMs)
    {
        CommandDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("command", commandName));

        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.CommandEnd,
            CommandName = commandName,
            ActivityId = Activity.Current?.Id,
            DurationMs = durationMs
        });
    }

    public static void EmitCommandError(string commandName, string errorCode, string errorMessage, double durationMs)
    {
        CommandErrorCounter.Add(1,
            new KeyValuePair<string, object?>("command", commandName),
            new KeyValuePair<string, object?>("error_code", errorCode));

        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.CommandError,
            CommandName = commandName,
            ActivityId = Activity.Current?.Id,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        });
    }

    public static void EmitDatabaseOpened(string dbPath, string dbPathSource, int schemaVersion, bool vecLoaded)
    {
        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.DatabaseOpened,
            ActivityId = Activity.Current?.Id,
            DbPath = dbPath,
            DbPathSource = dbPathSource,
            SchemaVersion = schemaVersion,
            VecLoaded = vecLoaded
        });
    }

    public static void EmitSearchExecuted(string searchMode)
    {
        SearchExecutionCounter.Add(1, new KeyValuePair<string, object?>("mode", searchMode));

        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.SearchExecuted,
            ActivityId = Activity.Current?.Id,
            SearchMode = searchMode
        });
    }

    public static void EmitIndexExecuted(string indexingMode)
    {
        IndexExecutionCounter.Add(1, new KeyValuePair<string, object?>("mode", indexingMode));

        EmitEvent(new DiagnosticEvent
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            EventType = DiagnosticEventType.IndexExecuted,
            ActivityId = Activity.Current?.Id,
            IndexingMode = indexingMode
        });
    }
}
