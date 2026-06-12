namespace PrintSink.Pdl;

/// <summary>
/// Identifies a supported PDL conversion.
/// </summary>
public enum PdlConversionKind
{
    /// <summary>
    /// Converts XPS or OXPS to PDF.
    /// </summary>
    XpsToPdf,

    /// <summary>
    /// Converts XPS or OXPS to PWG Raster.
    /// </summary>
    XpsToPwgRaster,
}
