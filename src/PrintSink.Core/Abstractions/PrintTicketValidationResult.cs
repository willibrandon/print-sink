namespace PrintSink.Abstractions;

/// <summary>
/// Describes the result of print ticket validation.
/// </summary>
public sealed class PrintTicketValidationResult
{
    private PrintTicketValidationResult(bool isResolved, string? message)
    {
        IsResolved = isResolved;
        Message = message;
    }

    /// <summary>
    /// Gets a value indicating whether the ticket was resolved.
    /// </summary>
    public bool IsResolved { get; }

    /// <summary>
    /// Gets a diagnostic message when validation did not resolve the ticket.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Creates a resolved validation result.
    /// </summary>
    /// <returns>A resolved validation result.</returns>
    public static PrintTicketValidationResult Resolved()
    {
        return new PrintTicketValidationResult(true, null);
    }

    /// <summary>
    /// Creates an unresolved validation result.
    /// </summary>
    /// <param name="message">The validation diagnostic.</param>
    /// <returns>An unresolved validation result.</returns>
    public static PrintTicketValidationResult Unresolved(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new PrintTicketValidationResult(false, message);
    }
}
