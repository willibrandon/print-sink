namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes the result status of an IPP printer-attribute read.
/// </summary>
public enum IppAttributeReadStatus
{
    /// <summary>
    /// The attribute read succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The target printer does not support the requested attribute read.
    /// </summary>
    NotSupported,

    /// <summary>
    /// The attribute read failed for a reason other than unsupported semantics.
    /// </summary>
    Failed,
}
