namespace PrintSink.Cli.Tui;

internal sealed class TuiPackageCommandResult
{
    internal TuiPackageCommandResult(int exitCode, string output, string error)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        ExitCode = exitCode;
        Output = output;
        Error = error;
    }

    internal int ExitCode { get; }

    internal string Output { get; }

    internal string Error { get; }
}
