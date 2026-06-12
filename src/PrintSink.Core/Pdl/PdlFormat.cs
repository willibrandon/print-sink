namespace PrintSink.Pdl;

/// <summary>
/// Identifies page description languages that PrintSink can receive, pass through, or produce.
/// </summary>
public enum PdlFormat
{
    /// <summary>
    /// The format is not recognized by PrintSink.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Open XML Paper Specification content.
    /// </summary>
    Oxps,

    /// <summary>
    /// Portable Document Format content.
    /// </summary>
    Pdf,

    /// <summary>
    /// PostScript content.
    /// </summary>
    PostScript,

    /// <summary>
    /// XML Paper Specification content.
    /// </summary>
    Xps,

    /// <summary>
    /// PWG Raster content.
    /// </summary>
    PwgRaster,

    /// <summary>
    /// Printer Command Language mobile content.
    /// </summary>
    Pclm,
}
