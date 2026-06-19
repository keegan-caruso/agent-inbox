namespace AgentInbox.Database;

// Character-trigram bag-of-words fallback — deterministic, no external deps, AOT-safe.
// Quality is adequate for keyword-level similarity; for true semantic search use OnnxEmbeddingGenerator.
internal sealed class EmbeddingGenerator : IEmbeddingGenerator
{
    public static readonly EmbeddingGenerator Instance = new();

    public int Dimensions => 384;

    public float[] Generate(string text)
    {
        var vector = new float[Dimensions];
        var lower = text.ToLowerInvariant();

        for (var i = 0; i <= lower.Length - 3; i++)
        {
            var bucket = (int)((uint)FnvHash(lower, i, 3) % Dimensions);
            vector[bucket] += 1.0f;
        }

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
