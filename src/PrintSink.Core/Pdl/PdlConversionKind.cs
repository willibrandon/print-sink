namespace PrintSink.Pdl;

/// <summary>
/// Identifies the Windows print workflow PDL conversion operation required for a job.
/// </summary>
public enum PdlConversionKind
{
    /// <summary>
    /// No converter is required.
    /// </summary>
    None = 0,

    /// <summary>
    /// Convert OXPS content to PDF.
    /// </summary>
    XpsToPdf,

    /// <summary>
    /// Convert OXPS content to PWG Raster.
    /// </summary>
    XpsToPwgr,

    /// <summary>
    /// Convert OXPS content to PCLm.
    /// </summary>
    XpsToPclm,
}
