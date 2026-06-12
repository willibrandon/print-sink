namespace PrintSink.Processing;

/// <summary>
/// Describes the outcome of virtual printer job processing.
/// </summary>
public enum VirtualPrinterProcessingStatus
{
    /// <summary>
    /// The job completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The job was canceled.
    /// </summary>
    Canceled,

    /// <summary>
    /// The job was rejected because no supported PDL plan exists.
    /// </summary>
    Rejected,
}
