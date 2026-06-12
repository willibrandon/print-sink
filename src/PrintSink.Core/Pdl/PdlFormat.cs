namespace PrintSink.Pdl;

/// <summary>
/// Identifies a PDL format.
/// </summary>
public enum PdlFormat
{
    /// <summary>
    /// Open XML Paper Specification.
    /// </summary>
    Oxps,

    /// <summary>
    /// XML Paper Specification.
    /// </summary>
    Xps,

    /// <summary>
    /// Portable Document Format.
    /// </summary>
    Pdf,

    /// <summary>
    /// PostScript.
    /// </summary>
    PostScript,

    /// <summary>
    /// PWG Raster.
    /// </summary>
    PwgRaster,
}
