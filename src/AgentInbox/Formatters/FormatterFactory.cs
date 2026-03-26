namespace AgentInbox.Formatters;

public static class FormatterFactory
{
    public static IOutputFormatter Create(OutputFormat format) => format switch
    {
        OutputFormat.Json => new JsonFormatter(),
        OutputFormat.Ndjson => new NdjsonFormatter(),
        _ => new PlainTextFormatter()
    };
}
