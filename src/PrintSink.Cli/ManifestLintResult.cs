namespace PrintSink.Cli;

/// <summary>
/// Captures the result of manifest linting.
/// </summary>
internal sealed class ManifestLintResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManifestLintResult"/> class.
    /// </summary>
    /// <param name="succeeded">A value indicating whether linting succeeded.</param>
    /// <param name="messages">The lint messages.</param>
    public ManifestLintResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    /// <summary>
    /// Gets a value indicating whether linting succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the lint messages.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }
}
