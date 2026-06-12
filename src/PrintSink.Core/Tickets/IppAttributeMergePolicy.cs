namespace PrintSink.Tickets;

/// <summary>
/// Describes how mapped IPP attributes should be merged with printer-provided attributes.
/// </summary>
public enum IppAttributeMergePolicy
{
    /// <summary>
    /// Preserve printer-provided attributes when duplicates exist.
    /// </summary>
    PreservePrinter,

    /// <summary>
    /// Replace printer-provided attributes with PrintSink-mapped values.
    /// </summary>
    Replace,

    /// <summary>
    /// Append mapped values to compatible printer-provided attributes.
    /// </summary>
    Append,
}
