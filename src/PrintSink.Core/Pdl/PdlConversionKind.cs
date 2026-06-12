namespace PrintSink.Core.Pdl;

/// <summary>
/// Identifies a Windows print workflow PDL conversion.
/// </summary>
public enum PdlConversionKind
{
    /// <summary>
    /// Convert XPS-family input to PDF.
    /// </summary>
    XpsToPdf,

    /// <summary>
    /// Convert XPS-family input to PWG Raster.
    /// </summary>
    XpsToPwgRaster,

    /// <summary>
    /// Convert XPS-family input to PCLm.
    /// </summary>
    XpsToPclm,
}
