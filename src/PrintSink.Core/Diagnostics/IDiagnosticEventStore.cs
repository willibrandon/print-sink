namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Persists recent PrintSink diagnostic events for local tools.
/// </summary>
public interface IDiagnosticEventStore
{
    /// <summary>
    /// Appends a diagnostic event.
    /// </summary>
    /// <param name="record">The diagnostic event to append.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the event is saved.</returns>
    Task AppendAsync(DiagnosticEventRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads recent diagnostic events.
    /// </summary>
    /// <param name="maxCount">The maximum number of events to return.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The most recent events first.</returns>
    Task<IReadOnlyList<DiagnosticEventRecord>> ReadRecentAsync(
        int maxCount,
        CancellationToken cancellationToken = default);
}
