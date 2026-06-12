namespace PrintSink.Endpoints;

/// <summary>
/// Identifies a PrintSink virtual endpoint.
/// </summary>
public enum EndpointKind
{
  /// <summary>
  /// Writes or passes through PDF output.
  /// </summary>
  Pdf,

  /// <summary>
  /// Writes XPS or OXPS output.
  /// </summary>
  Xps,

  /// <summary>
  /// Writes PostScript output.
  /// </summary>
  PostScript,

  /// <summary>
  /// Sends output to a custom non-file sink.
  /// </summary>
  Cloud,

  /// <summary>
  /// Writes PWG Raster output.
  /// </summary>
  PwgRaster,
}
