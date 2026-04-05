namespace AgentInbox.Database;

/// <summary>
/// Generates a simple character-trigram bag-of-words embedding vector.
/// The algorithm is fully deterministic and requires no external dependencies,
/// making it suitable for Native AOT and offline use. Quality is adequate for
/// keyword-level similarity; for higher-quality semantic search provide
/// pre-computed embeddings via --embedding.
/// </summary>
internal static class EmbeddingGenerator
{
    public const int Dimensions = 384;

    public static float[] Generate(string text)
    {
        var vector = new float[Dimensions];
        var lower = text.ToLowerInvariant();

        // Character trigrams (weight 1.0)
        for (var i = 0; i <= lower.Length - 3; i++)
        {
            var bucket = (int)((uint)FnvHash(lower, i, 3) % Dimensions);
            vector[bucket] += 1.0f;
        }

        // Character unigrams (weight 0.5) to handle very short texts
        for (var i = 0; i < lower.Length; i++)
        {
            var bucket = (int)((uint)FnvHash(lower, i, 1) % Dimensions);
            vector[bucket] += 0.5f;
        }

        NormalizeInPlace(vector);
        return vector;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        var sum = 0.0f;
        foreach (var v in vector)
            sum += v * v;

        if (sum <= 0f) return;

        var magnitude = MathF.Sqrt(sum);
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= magnitude;
    }

    private static int FnvHash(string text, int start, int length)
    {
        unchecked
        {
            var hash = (int)2166136261u;
            for (var i = start; i < start + length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
