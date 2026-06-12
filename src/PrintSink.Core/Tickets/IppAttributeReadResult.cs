namespace PrintSink.Core.Tickets;

/// <summary>
/// Captures IPP printer attributes read from a print device.
/// </summary>
public sealed class IppAttributeReadResult
{
    private IppAttributeReadResult(
        IppAttributeReadStatus status,
        IReadOnlyDictionary<string, IppAttributeValue> attributes,
        string? message)
    {
        Status = status;
        Attributes = attributes;
        Message = message;
    }

    /// <summary>
    /// Gets the read status.
    /// </summary>
    public IppAttributeReadStatus Status { get; }

    /// <summary>
    /// Gets the attributes returned by the printer.
    /// </summary>
    public IReadOnlyDictionary<string, IppAttributeValue> Attributes { get; }

    /// <summary>
    /// Gets the diagnostic message for unsupported or failed reads.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Creates a successful read result.
    /// </summary>
    /// <param name="attributes">The returned IPP printer attributes.</param>
    /// <returns>The successful read result.</returns>
    public static IppAttributeReadResult Success(IReadOnlyDictionary<string, IppAttributeValue> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        return new IppAttributeReadResult(
            IppAttributeReadStatus.Succeeded,
            new Dictionary<string, IppAttributeValue>(attributes, StringComparer.OrdinalIgnoreCase),
            null);
    }

    /// <summary>
    /// Creates an unsupported read result.
    /// </summary>
    /// <param name="message">The optional diagnostic message.</param>
    /// <returns>The unsupported read result.</returns>
    public static IppAttributeReadResult NotSupported(string? message = null)
    {
        return new IppAttributeReadResult(
            IppAttributeReadStatus.NotSupported,
            new Dictionary<string, IppAttributeValue>(StringComparer.OrdinalIgnoreCase),
            message);
    }

    /// <summary>
    /// Creates a failed read result.
    /// </summary>
    /// <param name="message">The optional diagnostic message.</param>
    /// <returns>The failed read result.</returns>
    public static IppAttributeReadResult Failed(string? message = null)
    {
        return new IppAttributeReadResult(
            IppAttributeReadStatus.Failed,
            new Dictionary<string, IppAttributeValue>(StringComparer.OrdinalIgnoreCase),
            message);
    }
}
