namespace PrintSink.Endpoints;

/// <summary>
/// Identifies the built-in endpoint behavior for a PrintSink virtual queue.
/// </summary>
public enum EndpointKind
{
    /// <summary>
    /// A file-backed endpoint that produces PDF output.
    /// </summary>
    PdfFile,

    /// <summary>
    /// A file-backed endpoint that produces XPS or OXPS output.
    /// </summary>
    XpsFile,

    /// <summary>
    /// A file-backed endpoint that produces PostScript output.
    /// </summary>
    PostScriptFile,

    /// <summary>
    /// A non-file endpoint that sends output to a cloud sink without the Save As broker.
    /// </summary>
    Cloud,

    /// <summary>
    /// A file-backed endpoint that produces PWG Raster output.
    /// </summary>
    PwgRasterFile,

    /// <summary>
    /// A file-backed endpoint that produces PCLm output.
    /// </summary>
    PclmFile,
}
