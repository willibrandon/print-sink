namespace PrintSink.Cli;

/// <summary>
/// Captures the result of print-ticket fixture mapping.
/// </summary>
internal sealed class TicketMapResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketMapResult"/> class.
    /// </summary>
    /// <param name="succeeded">A value indicating whether mapping succeeded.</param>
    /// <param name="messages">The mapping messages.</param>
    public TicketMapResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    /// <summary>
    /// Gets a value indicating whether mapping succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the mapping messages.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }
}
