using PrintSink.Endpoints;
using PrintSink.Watermark;

namespace PrintSink.Pdl;

/// <summary>
/// Resolves PDL copy and conversion behavior for PrintSink virtual endpoints.
/// </summary>
public sealed class PdlRouter : IPdlRouter
{
    /// <inheritdoc />
    public PdlPlan Resolve(string contentType, VirtualEndpoint endpoint, WatermarkOptions? watermarkOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(endpoint);

        PdlFormat sourceFormat = PdlFormatInfo.FromContentType(contentType);
        PdlFormat targetFormat = endpoint.TargetFormat;

        if (sourceFormat == PdlFormat.Unknown)
        {
            return PdlPlan.Reject(sourceFormat, targetFormat, $"Unsupported source content type '{contentType}'.");
        }

        bool wantsWatermark = watermarkOptions?.IsEnabled == true && PdlFormatInfo.IsXpsFamily(sourceFormat);

        if (sourceFormat == targetFormat && endpoint.SupportsPassthrough(sourceFormat))
        {
            return PdlPlan.Copy(sourceFormat, targetFormat, wantsWatermark);
        }

        if (sourceFormat == PdlFormat.Pdf && targetFormat == PdlFormat.Pdf && endpoint.SupportsPassthrough(sourceFormat))
        {
            return PdlPlan.Copy(sourceFormat, targetFormat);
        }

        if (sourceFormat == PdlFormat.PostScript && targetFormat == PdlFormat.PostScript && endpoint.SupportsPassthrough(sourceFormat))
        {
            return PdlPlan.Copy(sourceFormat, targetFormat);
        }

        if (sourceFormat == PdlFormat.Oxps)
        {
            return targetFormat switch
            {
                PdlFormat.Pdf => PdlPlan.Convert(sourceFormat, targetFormat, PdlConversionKind.XpsToPdf, wantsWatermark),
                PdlFormat.PwgRaster => PdlPlan.Convert(sourceFormat, targetFormat, PdlConversionKind.XpsToPwgr, wantsWatermark),
                PdlFormat.Pclm => PdlPlan.Convert(sourceFormat, targetFormat, PdlConversionKind.XpsToPclm, wantsWatermark),
                PdlFormat.Xps or PdlFormat.Oxps => PdlPlan.Copy(sourceFormat, targetFormat, wantsWatermark),
                _ => PdlPlan.Reject(sourceFormat, targetFormat, $"No OXPS conversion path exists for target '{targetFormat}'."),
            };
        }

        return PdlPlan.Reject(sourceFormat, targetFormat, $"Source format '{sourceFormat}' cannot produce endpoint target '{targetFormat}'.");
    }
}
