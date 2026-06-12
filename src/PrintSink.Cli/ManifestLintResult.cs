namespace PrintSink.Cli;

internal sealed class ManifestLintResult
{
    public ManifestLintResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Messages { get; }
}
