namespace PrintSink.Pdl;

/// <summary>
/// Describes the high-level action a virtual printer job must perform for a PDL payload.
/// </summary>
public enum PdlActionKind
{
    /// <summary>
    /// The source stream can be copied directly to the sink.
    /// </summary>
    Copy,

    /// <summary>
    /// The source stream must be converted by the Windows print workflow PDL converter.
    /// </summary>
    Convert,

    /// <summary>
    /// The source and target formats are not supported for this endpoint.
    /// </summary>
    Reject,
}
