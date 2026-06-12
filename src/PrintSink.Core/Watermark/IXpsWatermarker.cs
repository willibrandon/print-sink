using PrintSink.Core.Pdl;

namespace PrintSink.Core.Watermark;

/// <summary>
/// Applies configured watermarks to XPS-family PDL streams.
/// </summary>
public interface IXpsWatermarker
{
    /// <summary>
    /// Applies watermark options to an XPS-family source stream.
    /// </summary>
    /// <param name="source">The source stream positioned at the beginning.</param>
    /// <param name="sourceFormat">The XPS-family source format.</param>
    /// <param name="options">The enabled watermark options.</param>
    /// <param name="cancellationToken">A token that cancels watermarking.</param>
    /// <returns>The watermarked stream positioned at the beginning.</returns>
    Task<Stream> ApplyAsync(
        Stream source,
        PdlFormat sourceFormat,
        WatermarkOptions options,
        CancellationToken cancellationToken = default);
}
