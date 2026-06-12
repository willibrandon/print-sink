using PrintSink.Core.Pdl;
using PrintSink.Core.Watermark;
using PrintSink.Xps.Projections;

namespace PrintSink.Tasks;

/// <summary>
/// Adapts watermark options to the native XPS component projection.
/// </summary>
internal sealed class ProjectedXpsWatermarker : IXpsWatermarker
{
    /// <inheritdoc />
    public Task<Stream> ApplyAsync(
        Stream source,
        PdlFormat sourceFormat,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceFormat is not (PdlFormat.Oxps or PdlFormat.Xps))
        {
            throw new NotSupportedException(
                $"Projected XPS watermarking requires XPS-family source content, but the job supplied {sourceFormat}.");
        }

        NativeXpsPageWatermarker watermarker = CreateNativeWatermarker(options);
        return watermarker.ApplyAsync(source, cancellationToken);
    }

    private static NativeXpsPageWatermarker CreateNativeWatermarker(WatermarkOptions options)
    {
        NativeXpsPageWatermarker watermarker = new();

        if (options.Text is TextWatermark text)
        {
            watermarker.ApplyText(text);
        }

        return watermarker;
    }
}
