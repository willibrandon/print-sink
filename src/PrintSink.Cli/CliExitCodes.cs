namespace PrintSink.Cli;

/// <summary>
/// Defines process exit codes used by the CLI.
/// </summary>
internal static class CliExitCodes
{
    /// <summary>
    /// Indicates successful execution.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Indicates validation completed and found a problem.
    /// </summary>
    public const int ValidationFailed = 1;

    /// <summary>
    /// Indicates invalid command input.
    /// </summary>
    public const int UsageError = 2;
}
