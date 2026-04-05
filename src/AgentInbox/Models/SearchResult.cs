namespace AgentInbox.Models;

public sealed class SearchResult
{
    public long MessageId { get; init; }
    public required string SenderId { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
    public long? ReplyToId { get; init; }
    public required string CreatedAt { get; init; }
    public bool IsRead { get; init; }
    /// <summary>BM25 rank for text search (lower = more relevant) or cosine distance for semantic search (lower = more similar).</summary>
    public double Score { get; init; }
}
