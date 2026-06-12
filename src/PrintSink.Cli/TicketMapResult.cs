namespace PrintSink.Cli;

internal sealed class TicketMapResult
{
    public TicketMapResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Messages { get; }
}
