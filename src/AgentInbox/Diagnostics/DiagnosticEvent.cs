using System.Text.Json.Serialization;

namespace AgentInbox.Diagnostics;

/// <summary>
/// Represents a diagnostic event emitted during command execution.
/// Events are emitted as NDJSON when verbose mode is enabled.
/// </summary>
public sealed class DiagnosticEvent
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = "";

    [JsonPropertyName("commandName")]
    public string? CommandName { get; init; }

    [JsonPropertyName("activityId")]
    public string? ActivityId { get; init; }

    [JsonPropertyName("durationMs")]
    public double? DurationMs { get; init; }

    [JsonPropertyName("dbPath")]
    public string? DbPath { get; init; }

    [JsonPropertyName("dbPathSource")]
    public string? DbPathSource { get; init; }

    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("vecLoaded")]
    public bool? VecLoaded { get; init; }

    [JsonPropertyName("searchMode")]
    public string? SearchMode { get; init; }

    [JsonPropertyName("indexingMode")]
    public string? IndexingMode { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Event type constants for diagnostic events.
/// </summary>
public static class DiagnosticEventType
{
    public const string CommandStart = "command_start";
    public const string CommandEnd = "command_end";
    public const string CommandError = "command_error";
    public const string DatabaseOpened = "database_opened";
    public const string SearchExecuted = "search_executed";
    public const string IndexExecuted = "index_executed";
}
