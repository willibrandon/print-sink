namespace PrintSink.Core.Pdl;

/// <summary>
/// Describes how PrintSink should handle a source PDL stream.
/// </summary>
public enum PdlActionKind
{
    /// <summary>
    /// Copy the stream to the sink without conversion.
    /// </summary>
    Copy,

    /// <summary>
    /// Convert the stream before writing it to the sink.
    /// </summary>
    Convert,

    /// <summary>
    /// Reject the stream because no supported route exists.
    /// </summary>
    Reject,
}
