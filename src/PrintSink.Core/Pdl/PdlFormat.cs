namespace PrintSink.Core.Pdl;

/// <summary>
/// Identifies a page description language handled by PrintSink.
/// </summary>
public enum PdlFormat
{
    /// <summary>
    /// Open XML Paper Specification.
    /// </summary>
    Oxps,

    /// <summary>
    /// Microsoft XML Paper Specification.
    /// </summary>
    Xps,

    /// <summary>
    /// Portable Document Format.
    /// </summary>
    Pdf,

    /// <summary>
    /// Adobe PostScript.
    /// </summary>
    PostScript,

    /// <summary>
    /// PWG Raster image stream.
    /// </summary>
    PwgRaster,

    /// <summary>
    /// Printer Command Language Mobile stream.
    /// </summary>
    Pclm,
}
