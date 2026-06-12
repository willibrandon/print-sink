using PrintSink.Pdl;

namespace PrintSink.Processing;

/// <summary>
/// Describes the result of processing a virtual printer job.
/// </summary>
public sealed class VirtualPrinterProcessingResult
{
    private VirtualPrinterProcessingResult(VirtualPrinterProcessingStatus status, PdlPlan? plan, string? message)
    {
        Status = status;
        Plan = plan;
        Message = message;
    }

    /// <summary>
    /// Gets the processing status.
    /// </summary>
    public VirtualPrinterProcessingStatus Status { get; }

    /// <summary>
    /// Gets the resolved PDL plan when one was available.
    /// </summary>
    public PdlPlan? Plan { get; }

    /// <summary>
    /// Gets an optional diagnostic message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="plan">The completed PDL plan.</param>
    /// <returns>A successful result.</returns>
    public static VirtualPrinterProcessingResult Succeeded(PdlPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new VirtualPrinterProcessingResult(VirtualPrinterProcessingStatus.Succeeded, plan, null);
    }

    /// <summary>
    /// Creates a canceled result.
    /// </summary>
    /// <param name="plan">The plan active when cancellation occurred.</param>
    /// <returns>A canceled result.</returns>
    public static VirtualPrinterProcessingResult Canceled(PdlPlan? plan)
    {
        return new VirtualPrinterProcessingResult(VirtualPrinterProcessingStatus.Canceled, plan, "The virtual printer job was canceled.");
    }

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    /// <param name="plan">The rejected PDL plan.</param>
    /// <returns>A rejected result.</returns>
    public static VirtualPrinterProcessingResult Rejected(PdlPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new VirtualPrinterProcessingResult(VirtualPrinterProcessingStatus.Rejected, plan, plan.RejectionReason);
    }
}
