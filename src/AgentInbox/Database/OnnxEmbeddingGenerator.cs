using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace AgentInbox.Database;

// Semantic embedding using bge-small-en-v1.5 via ONNX Runtime.
// Requires the model file (~33 MB) and vocab.txt from BAAI/bge-small-en-v1.5 on Hugging Face.
// Default paths: ~/.agent-inbox/models/bge-small-en-v1.5.onnx + vocab.txt in same directory.
// Falls back to the trigram EmbeddingGenerator when the model files are absent.
internal sealed class OnnxEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    public const int ModelDimensions = 384;
    private const int MaxTokens = 512;

    public int Dimensions => ModelDimensions;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly bool _hasTokenTypeIds;

    private OnnxEmbeddingGenerator(InferenceSession session, BertTokenizer tokenizer)
    {
        _session = session;
        _tokenizer = tokenizer;
        _hasTokenTypeIds = session.InputMetadata.ContainsKey("token_type_ids");
    }

    public static string DefaultModelPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agent-inbox", "models", "bge-small-en-v1.5.onnx");

    public static string DefaultVocabPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agent-inbox", "models", "vocab.txt");

    public static OnnxEmbeddingGenerator? TryCreate(
        string? modelPath = null,
        string? vocabPath = null)
    {
        modelPath ??= DefaultModelPath;
        vocabPath ??= DefaultVocabPath;

        if (!File.Exists(modelPath) || !File.Exists(vocabPath))
            return null;

        try
        {
            var options = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
            var session = new InferenceSession(modelPath, options);
            var tokenizer = BertTokenizer.Create(vocabPath, new BertTokenizerOptions { LowerCaseBeforeTokenize = false });
            return new OnnxEmbeddingGenerator(session, tokenizer);
        }
        catch
        {
            return null;
        }
    }

    public float[] Generate(string text)
    {
        var ids = _tokenizer.EncodeToIds(text, addSpecialTokens: true, maxTokenCount: MaxTokens);
        var seqLen = ids.Count;

        var inputIds = new long[seqLen];
        var attentionMask = new long[seqLen];

        for (var i = 0; i < seqLen; i++)
        {
            inputIds[i] = ids[i];
            attentionMask[i] = 1;
        }

        var shape = new long[] { 1, seqLen };

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(attentionMask, shape)),
        };

        if (_hasTokenTypeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(new long[seqLen], shape)));
        }

        using var outputs = _session.Run(inputs);

        // Prefer sentence_embedding (pre-pooled) if the export includes it;
        // otherwise mean-pool last_hidden_state across the sequence dimension.
        var sentenceEmbeddingResult = outputs.FirstOrDefault(o => o.Name == "sentence_embedding");
        if (sentenceEmbeddingResult is not null)
        {
            var pooled = sentenceEmbeddingResult.AsEnumerable<float>().ToArray();
            NormalizeInPlace(pooled);
            return pooled;
        }

        var hiddenState = outputs.First(o => o.Name == "last_hidden_state")
                                 .AsEnumerable<float>()
                                 .ToArray();

        return MeanPoolAndNormalize(hiddenState, seqLen, ModelDimensions);
    }

    private static float[] MeanPoolAndNormalize(float[] hiddenState, int seqLen, int dims)
    {
        var embedding = new float[dims];
        for (var t = 0; t < seqLen; t++)
            for (var d = 0; d < dims; d++)
                embedding[d] += hiddenState[t * dims + d];

        for (var d = 0; d < dims; d++)
            embedding[d] /= seqLen;

        NormalizeInPlace(embedding);
        return embedding;
    }

    private static void NormalizeInPlace(float[] v)
    {
        var sum = 0f;
        foreach (var x in v) sum += x * x;
        if (sum <= 0f) return;
        var mag = MathF.Sqrt(sum);
        for (var i = 0; i < v.Length; i++) v[i] /= mag;
    }

    public void Dispose() => _session.Dispose();
}
