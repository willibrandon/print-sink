namespace PrintSink.Cli;

/// <summary>
/// Carries CLI services shared by command handlers.
/// </summary>
internal sealed class CliContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CliContext"/> class.
    /// </summary>
    /// <param name="output">The standard-output writer.</param>
    /// <param name="error">The standard-error writer.</param>
    /// <param name="workingDirectory">The current working directory for relative paths.</param>
    public CliContext(TextWriter output, TextWriter error, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        Output = output;
        Error = error;
        WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// Gets the standard-output writer.
    /// </summary>
    public TextWriter Output { get; }

    /// <summary>
    /// Gets the standard-error writer.
    /// </summary>
    public TextWriter Error { get; }

    /// <summary>
    /// Gets the current working directory for relative paths.
    /// </summary>
    public string WorkingDirectory { get; }
}
