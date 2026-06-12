namespace PrintSink.Cli;

/// <summary>
/// Captures the installed printer queue names visible to the CLI.
/// </summary>
internal sealed class PrinterQueueSnapshot
{
    private PrinterQueueSnapshot(HashSet<string> queueNames, string? unavailableReason)
    {
        QueueNames = queueNames;
        UnavailableReason = unavailableReason;
    }

    internal bool IsAvailable => UnavailableReason is null;

    internal string? UnavailableReason { get; }

    private HashSet<string> QueueNames { get; }

    internal static PrinterQueueSnapshot Available(IEnumerable<string> queueNames)
    {
        ArgumentNullException.ThrowIfNull(queueNames);

        return new PrinterQueueSnapshot(queueNames.ToHashSet(StringComparer.OrdinalIgnoreCase), null);
    }

    internal static PrinterQueueSnapshot Unavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new PrinterQueueSnapshot([], reason);
    }

    internal bool Contains(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return QueueNames.Contains(queueName);
    }
}
