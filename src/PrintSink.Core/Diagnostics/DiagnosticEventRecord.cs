using System.Text.Json.Serialization;

namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Captures a PrintSink diagnostic event that can be shown in local tooling.
/// </summary>
public sealed class DiagnosticEventRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticEventRecord"/> class.
    /// </summary>
    /// <param name="timestamp">The time the event occurred.</param>
    /// <param name="severity">The event severity.</param>
    /// <param name="source">The component that emitted the event.</param>
    /// <param name="message">The short event message.</param>
    /// <param name="endpoint">The virtual printer endpoint, when known.</param>
    /// <param name="detail">The optional event detail.</param>
    [JsonConstructor]
    public DiagnosticEventRecord(
        DateTimeOffset timestamp,
        DiagnosticEventSeverity severity,
        string source,
        string message,
        string? endpoint,
        string? detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Timestamp = timestamp;
        Severity = severity;
        Source = source;
        Message = message;
        Endpoint = endpoint;
        Detail = detail;
    }

    /// <summary>
    /// Gets the time the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the event severity.
    /// </summary>
    public DiagnosticEventSeverity Severity { get; }

    /// <summary>
    /// Gets the component that emitted the event.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the short event message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the virtual printer endpoint, when known.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Gets the optional event detail.
    /// </summary>
    public string? Detail { get; }
}
