namespace PrintSink.Pdl;

using PrintSink.Endpoints;

/// <summary>
/// Resolves copy, conversion, and rejection decisions for PDL streams.
/// </summary>
public sealed class PdlRouter : IPdlRouter
{
  /// <inheritdoc />
  public PdlPlan Resolve(string contentType, VirtualEndpoint endpoint)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
    ArgumentNullException.ThrowIfNull(endpoint);

    if (!PdlFormatInfo.TryParseContentType(contentType, out PdlFormat sourceFormat))
    {
      return new PdlPlan(PdlActionKind.Reject, null, endpoint.TargetFormat, null, "Unknown content type.");
    }

    if (endpoint.SupportsPassthrough(sourceFormat))
    {
      return new PdlPlan(PdlActionKind.Copy, sourceFormat, endpoint.TargetFormat, null, "Endpoint supports passthrough.");
    }

    if (sourceFormat is PdlFormat.Oxps or PdlFormat.Xps)
    {
      if (endpoint.TargetFormat is PdlFormat.Oxps or PdlFormat.Xps)
      {
        return new PdlPlan(PdlActionKind.Copy, sourceFormat, endpoint.TargetFormat, null, "XPS family passthrough.");
      }

      if (endpoint.TargetFormat is PdlFormat.Pdf)
      {
        return new PdlPlan(PdlActionKind.Convert, sourceFormat, endpoint.TargetFormat, PdlConversionKind.XpsToPdf, "Convert XPS to PDF.");
      }

      if (endpoint.TargetFormat is PdlFormat.PwgRaster)
      {
        return new PdlPlan(PdlActionKind.Convert, sourceFormat, endpoint.TargetFormat, PdlConversionKind.XpsToPwgRaster, "Convert XPS to PWG Raster.");
      }
    }

    return new PdlPlan(PdlActionKind.Reject, sourceFormat, endpoint.TargetFormat, null, "No supported route.");
  }
}
