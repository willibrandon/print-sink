namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Identifies the severity of a persisted PrintSink diagnostic event.
/// </summary>
public enum DiagnosticEventSeverity
{
    /// <summary>
    /// The event records normal operational progress.
    /// </summary>
    Information,

    /// <summary>
    /// The event records a recoverable or expected warning.
    /// </summary>
    Warning,

    /// <summary>
    /// The event records a failure.
    /// </summary>
    Error,
}
