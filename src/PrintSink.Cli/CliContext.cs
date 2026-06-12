namespace PrintSink.Cli;

internal sealed class CliContext
{
    public CliContext(TextWriter output, TextWriter error, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        Output = output;
        Error = error;
        WorkingDirectory = workingDirectory;
    }

    public TextWriter Output { get; }

    public TextWriter Error { get; }

    public string WorkingDirectory { get; }
}
