namespace PrintSink.Core.Settings;

/// <summary>
/// Describes whether foreground job UI should be launched before processing a print job.
/// </summary>
public sealed class JobUiOptions
{
    /// <summary>
    /// Gets the default job UI options.
    /// </summary>
    public static JobUiOptions Default { get; } = new(true);

    /// <summary>
    /// Initializes a new instance of the <see cref="JobUiOptions"/> class.
    /// </summary>
    /// <param name="launchJobUi">A value indicating whether the foreground job UI should be launched.</param>
    public JobUiOptions(bool launchJobUi)
    {
        LaunchJobUi = launchJobUi;
    }

    /// <summary>
    /// Gets a value indicating whether the foreground job UI should be launched.
    /// </summary>
    public bool LaunchJobUi { get; }
}
