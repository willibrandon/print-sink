namespace PrintSink.Core.Abstractions;

/// <summary>
/// Identifies the final status of a virtual printer job.
/// </summary>
public enum VirtualPrinterJobStatus
{
    /// <summary>
    /// The job completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The user or system canceled the job.
    /// </summary>
    Canceled,

    /// <summary>
    /// The job failed.
    /// </summary>
    Failed,
}
