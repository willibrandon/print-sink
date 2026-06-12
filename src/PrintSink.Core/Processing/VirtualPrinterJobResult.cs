using PrintSink.Core.Abstractions;
using PrintSink.Core.Pdl;

namespace PrintSink.Core.Processing;

/// <summary>
/// Captures the outcome of processing a virtual printer job.
/// </summary>
public sealed class VirtualPrinterJobResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobResult"/> class.
    /// </summary>
    /// <param name="plan">The selected PDL plan.</param>
    /// <param name="status">The final job status.</param>
    /// <param name="exception">The exception that caused failure, if any.</param>
    public VirtualPrinterJobResult(PdlPlan plan, VirtualPrinterJobStatus status, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Plan = plan;
        Status = status;
        Exception = exception;
    }

    /// <summary>
    /// Gets the selected PDL plan.
    /// </summary>
    public PdlPlan Plan { get; }

    /// <summary>
    /// Gets the final job status.
    /// </summary>
    public VirtualPrinterJobStatus Status { get; }

    /// <summary>
    /// Gets the exception that caused failure, if any.
    /// </summary>
    public Exception? Exception { get; }
}
