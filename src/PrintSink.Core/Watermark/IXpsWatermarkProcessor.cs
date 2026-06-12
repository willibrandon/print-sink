namespace PrintSink.Watermark;

/// <summary>
/// Applies PrintSink watermark options to XPS-family PDL content.
/// </summary>
public interface IXpsWatermarkProcessor
{
    /// <summary>
    /// Applies watermark options to an XPS-family source stream.
    /// </summary>
    /// <param name="source">The source XPS-family stream positioned at the beginning.</param>
    /// <param name="options">The effective watermark options.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A stream containing the watermarked XPS-family content positioned at the beginning.</returns>
    Task<Stream> ApplyAsync(Stream source, WatermarkOptions options, CancellationToken cancellationToken = default);
}
