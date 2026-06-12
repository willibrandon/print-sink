namespace PrintSink.Cli;

/// <summary>
/// Captures a validation result and its diagnostic messages.
/// </summary>
internal sealed class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="succeeded">A value indicating whether validation succeeded.</param>
    /// <param name="messages">The validation messages.</param>
    public ValidationResult(bool succeeded, IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Succeeded = succeeded;
        Messages = messages;
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the validation messages.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }
}
