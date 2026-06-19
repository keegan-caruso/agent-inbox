namespace AgentInbox.Database;

internal interface IEmbeddingGenerator
{
    int Dimensions { get; }
    float[] Generate(string text);
}
