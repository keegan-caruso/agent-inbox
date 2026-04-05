namespace AgentInbox.Database;

/// <summary>
/// Simple keyword-based embedding generator for message content.
/// This uses a basic character n-gram approach to create 32-dimensional embeddings
/// that can be used for semantic similarity search.
/// </summary>
public static class EmbeddingGenerator
{
    private const int EmbeddingDimensions = 32;

    /// <summary>
    /// Generate a 32-dimensional embedding for message content (subject + body).
    /// Uses character n-grams and simple hashing to create a fixed-size vector.
    /// </summary>
    public static float[] GenerateEmbedding(string text)
    {
        var embedding = new float[EmbeddingDimensions];
        var normalized = text.ToLowerInvariant();

        // Character unigrams (individual characters)
        foreach (var c in normalized)
        {
            var hash = Math.Abs(c.GetHashCode() % EmbeddingDimensions);
            embedding[hash] += 1.0f;
        }

        // Character bigrams (pairs of characters)
        for (var i = 0; i < normalized.Length - 1; i++)
        {
            var bigram = normalized.Substring(i, 2);
            var hash = Math.Abs(bigram.GetHashCode() % EmbeddingDimensions);
            embedding[hash] += 0.5f;
        }

        // Character trigrams (triples of characters)
        for (var i = 0; i < normalized.Length - 2; i++)
        {
            var trigram = normalized.Substring(i, 3);
            var hash = Math.Abs(trigram.GetHashCode() % EmbeddingDimensions);
            embedding[hash] += 0.3f;
        }

        // Normalize the embedding to unit length (L2 normalization)
        var magnitude = 0.0f;
        for (var i = 0; i < EmbeddingDimensions; i++)
            magnitude += embedding[i] * embedding[i];

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude > 0)
        {
            for (var i = 0; i < EmbeddingDimensions; i++)
                embedding[i] /= magnitude;
        }

        return embedding;
    }

    /// <summary>
    /// Format an embedding as a JSON array string for SQLite vec.
    /// </summary>
    public static string FormatEmbedding(float[] embedding)
    {
        return "[" + string.Join(",", embedding.Select(v => v.ToString("F6"))) + "]";
    }
}
