using System.Diagnostics.Tracing;
using PrintSink.Pdl;

namespace PrintSink.Diagnostics;

/// <summary>
/// Emits ETW events for PrintSink job lifecycle and routing decisions.
/// </summary>
[EventSource(Name = "PrintSink-Diagnostics")]
public sealed class PrintSinkEventSource : EventSource
{
    /// <summary>
    /// Gets the singleton event source instance.
    /// </summary>
    public static PrintSinkEventSource Log { get; } = new();

    private PrintSinkEventSource()
    {
    }

    /// <summary>
    /// Emits a job-started event.
    /// </summary>
    /// <param name="endpointKind">The endpoint kind.</param>
    /// <param name="contentType">The source content type.</param>
    [Event(1, Level = EventLevel.Informational, Message = "Job started for endpoint {0} with content type {1}.")]
    public void JobStarted(string endpointKind, string contentType)
    {
        if (IsEnabled())
        {
            WriteEvent(1, endpointKind, contentType);
        }
    }

    /// <summary>
    /// Emits a PDL-plan-resolved event.
    /// </summary>
    /// <param name="action">The resolved PDL action.</param>
    /// <param name="sourceFormat">The source format.</param>
    /// <param name="targetFormat">The target format.</param>
    /// <param name="conversion">The conversion kind.</param>
    /// <param name="requiresWatermark">Whether watermarking is required.</param>
    [Event(2, Level = EventLevel.Informational, Message = "PDL plan {0}: {1} -> {2}, converter {3}, watermark {4}.")]
    public void PdlPlanResolved(string action, string sourceFormat, string targetFormat, string conversion, bool requiresWatermark)
    {
        if (IsEnabled())
        {
            WriteEvent(2, action, sourceFormat, targetFormat, conversion, requiresWatermark);
        }
    }

    /// <summary>
    /// Emits a PDL-plan-resolved event from a plan object.
    /// </summary>
    /// <param name="plan">The resolved plan.</param>
    public void PdlPlanResolved(PdlPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        PdlPlanResolved(
            plan.Action.ToString(),
            plan.SourceFormat.ToString(),
            plan.TargetFormat.ToString(),
            plan.Conversion.ToString(),
            plan.RequiresWatermark);
    }

    /// <summary>
    /// Emits a job-completed event.
    /// </summary>
    /// <param name="endpointKind">The endpoint kind.</param>
    /// <param name="status">The submitted status reported to the print workflow.</param>
    /// <param name="elapsedMilliseconds">The elapsed job time in milliseconds.</param>
    [Event(3, Level = EventLevel.Informational, Message = "Job completed for endpoint {0} with status {1} in {2} ms.")]
    public void JobCompleted(string endpointKind, string status, long elapsedMilliseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(3, endpointKind, status, elapsedMilliseconds);
        }
    }

    /// <summary>
    /// Emits a job-failed event.
    /// </summary>
    /// <param name="endpointKind">The endpoint kind.</param>
    /// <param name="errorCode">The normalized error code.</param>
    /// <param name="message">The diagnostic message.</param>
    [Event(4, Level = EventLevel.Error, Message = "Job failed for endpoint {0}: {1} {2}.")]
    public void JobFailed(string endpointKind, string errorCode, string message)
    {
        if (IsEnabled())
        {
            WriteEvent(4, endpointKind, errorCode, message);
        }
    }
}
