using PrintSink.Core.Endpoints;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Pdl;

/// <summary>
/// Applies XPS-family watermarking before conversion or sink writes.
/// </summary>
public sealed class XpsWatermarkPdlTransformer : IPdlTransformer
{
    private readonly IXpsWatermarker watermarker;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpsWatermarkPdlTransformer"/> class.
    /// </summary>
    /// <param name="watermarker">The XPS watermarking adapter.</param>
    public XpsWatermarkPdlTransformer(IXpsWatermarker watermarker)
    {
        ArgumentNullException.ThrowIfNull(watermarker);

        this.watermarker = watermarker;
    }

    /// <inheritdoc />
    public async Task<Stream> TransformAsync(
        Stream source,
        VirtualEndpoint endpoint,
        PdlPlan plan,
        WatermarkOptions watermarkOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(watermarkOptions);

        if (!watermarkOptions.Enabled)
        {
            return source;
        }

        PdlFormat sourceFormat = plan.SourceFormat
            ?? throw new NotSupportedException("Watermarking requires a known XPS-family source format.");
        if (sourceFormat is not (PdlFormat.Oxps or PdlFormat.Xps))
        {
            throw new NotSupportedException(
                $"Watermarking requires XPS-family source content, but the job supplied {sourceFormat}.");
        }

        Stream result = await watermarker
            .ApplyAsync(source, sourceFormat, watermarkOptions, cancellationToken)
            .ConfigureAwait(false);
        if (result.CanSeek)
        {
            result.Position = 0;
        }

        return result;
    }
}
