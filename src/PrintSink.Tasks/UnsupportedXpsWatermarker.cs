using PrintSink.Core.Pdl;
using PrintSink.Core.Watermark;

namespace PrintSink.Tasks;

/// <summary>
/// Reports missing native XPS watermarking support until the packaged XPS component is available.
/// </summary>
internal sealed class UnsupportedXpsWatermarker : IXpsWatermarker
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

        throw new NotSupportedException(
            "XPS watermarking requires the PrintSink.Xps package component, which is not wired into this build yet.");
    }
}
