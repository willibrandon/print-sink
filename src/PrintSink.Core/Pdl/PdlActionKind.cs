namespace PrintSink.Pdl;

/// <summary>
/// Describes the action required for a PDL stream.
/// </summary>
public enum PdlActionKind
{
    /// <summary>
    /// Copy the source stream directly.
    /// </summary>
    Copy,

    /// <summary>
    /// Convert the source stream before writing.
    /// </summary>
    Convert,

    /// <summary>
    /// Reject the stream because no supported route exists.
    /// </summary>
    Reject,
}
