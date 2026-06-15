namespace PrintSink.Core.Settings;

/// <summary>
/// Describes whether foreground job UI should be launched before processing a print job.
/// </summary>
/// <param name="launchJobUi">A value indicating whether the foreground job UI should be launched.</param>
public sealed class JobUiOptions(bool launchJobUi)
{
    /// <summary>
    /// Gets the default job UI options.
    /// </summary>
    public static JobUiOptions Default { get; } = new(true);

    /// <summary>
    /// Gets a value indicating whether the foreground job UI should be launched.
    /// </summary>
    public bool LaunchJobUi { get; } = launchJobUi;
}
