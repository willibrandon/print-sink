using System.Text;
using PrintSink.Watermark;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Records XPS watermark calls for processor tests.
/// </summary>
public sealed class RecordingXpsWatermarkProcessor : IXpsWatermarkProcessor
{
    /// <summary>
    /// Gets a value indicating whether watermarking was invoked.
    /// </summary>
    public bool WasInvoked { get; private set; }

    /// <summary>
    /// Gets the source bytes observed by the watermarker.
    /// </summary>
    public byte[] SourceBytes { get; private set; } = Array.Empty<byte>();

    /// <inheritdoc />
    public async Task<Stream> ApplyAsync(Stream source, WatermarkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        WasInvoked = true;
        using MemoryStream buffer = new();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        SourceBytes = buffer.ToArray();

        byte[] prefix = Encoding.UTF8.GetBytes("watermarked:");
        MemoryStream output = new();
        await output.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(SourceBytes, cancellationToken).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }
}
