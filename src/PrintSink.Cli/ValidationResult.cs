namespace PrintSink.Cli;

internal sealed class ValidationResult
{
    public ValidationResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Messages { get; }
}
