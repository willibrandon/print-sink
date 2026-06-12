using System.Diagnostics.Tracing;

namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Emits PrintSink diagnostic events for ETW/EventSource listeners.
/// </summary>
[EventSource(Name = "PrintSink-Diagnostics")]
public sealed class PrintSinkDiagnostics : EventSource
{
    /// <summary>
    /// Gets the shared PrintSink diagnostic event source.
    /// </summary>
    public static PrintSinkDiagnostics Log { get; } = new();

    private PrintSinkDiagnostics()
    {
    }

    /// <summary>
    /// Emits a routing decision event.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="contentType">The source content type.</param>
    /// <param name="action">The selected action.</param>
    /// <param name="sourceFormat">The parsed source format.</param>
    /// <param name="targetFormat">The target format.</param>
    /// <param name="conversionKind">The conversion kind.</param>
    /// <param name="reason">The routing reason.</param>
    [Event(1, Level = EventLevel.Informational)]
    public void JobRouteResolved(
        string endpoint,
        string contentType,
        string action,
        string sourceFormat,
        string targetFormat,
        string conversionKind,
        string reason)
    {
        if (IsEnabled())
        {
            WriteEvent(1, endpoint, contentType, action, sourceFormat, targetFormat, conversionKind, reason);
        }
    }

    /// <summary>
    /// Emits an event before conversion starts.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="conversionKind">The conversion kind.</param>
    [Event(2, Level = EventLevel.Informational)]
    public void PdlConversionStarted(string endpoint, string conversionKind)
    {
        if (IsEnabled())
        {
            WriteEvent(2, endpoint, conversionKind);
        }
    }

    /// <summary>
    /// Emits an event after conversion completes.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="conversionKind">The conversion kind.</param>
    /// <param name="elapsedMilliseconds">The elapsed conversion time in milliseconds.</param>
    [Event(3, Level = EventLevel.Informational)]
    public void PdlConversionCompleted(string endpoint, string conversionKind, long elapsedMilliseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(3, endpoint, conversionKind, elapsedMilliseconds);
        }
    }

    /// <summary>
    /// Emits a successful completion event.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="status">The final job status.</param>
    /// <param name="elapsedMilliseconds">The elapsed processing time in milliseconds.</param>
    [Event(4, Level = EventLevel.Informational)]
    public void JobCompleted(string endpoint, string status, long elapsedMilliseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(4, endpoint, status, elapsedMilliseconds);
        }
    }

    /// <summary>
    /// Emits a failed completion event.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="exceptionType">The exception type.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="elapsedMilliseconds">The elapsed processing time in milliseconds.</param>
    [Event(5, Level = EventLevel.Error)]
    public void JobFailed(string endpoint, string exceptionType, string message, long elapsedMilliseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(5, endpoint, exceptionType, message, elapsedMilliseconds);
        }
    }

    /// <summary>
    /// Emits a rejected job event.
    /// </summary>
    /// <param name="endpoint">The endpoint queue name.</param>
    /// <param name="reason">The rejection reason.</param>
    /// <param name="elapsedMilliseconds">The elapsed processing time in milliseconds.</param>
    [Event(6, Level = EventLevel.Warning)]
    public void JobRejected(string endpoint, string reason, long elapsedMilliseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(6, endpoint, reason, elapsedMilliseconds);
        }
    }
}
