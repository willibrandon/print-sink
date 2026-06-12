using PrintSink.Abstractions;

namespace PrintSink.Processing;

/// <summary>
/// Processes print-stack-neutral virtual printer jobs.
/// </summary>
public interface IVirtualPrinterJobProcessor
{
    /// <summary>
    /// Processes a virtual printer job.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The processing result.</returns>
    Task<VirtualPrinterProcessingResult> ProcessAsync(IVirtualPrinterJob job, CancellationToken cancellationToken = default);
}
