using PrintSink.Core.Watermark;

namespace PrintSink.Core.Settings;

/// <summary>
/// Describes per-job processing options captured by foreground job UI.
/// </summary>
public sealed class JobProcessingOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobProcessingOptions"/> class.
    /// </summary>
    /// <param name="watermarkOptions">The watermark options for this job.</param>
    /// <param name="jobPasswordOptions">The IPP job password options for this job.</param>
    public JobProcessingOptions(WatermarkOptions watermarkOptions, JobPasswordOptions? jobPasswordOptions = null)
    {
        ArgumentNullException.ThrowIfNull(watermarkOptions);

        WatermarkOptions = watermarkOptions;
        JobPasswordOptions = jobPasswordOptions;
    }

    /// <summary>
    /// Gets the watermark options for this job.
    /// </summary>
    public WatermarkOptions WatermarkOptions { get; }

    /// <summary>
    /// Gets the IPP job password options for this job.
    /// </summary>
    public JobPasswordOptions? JobPasswordOptions { get; }
}
