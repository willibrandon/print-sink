using PrintSink.Core.Pdl;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Captures XPS watermark requests for transformer tests.
/// </summary>
internal sealed class TestXpsWatermarker : IXpsWatermarker
{
    private readonly byte[] outputBytes;
    private readonly Exception? exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestXpsWatermarker"/> class.
    /// </summary>
    /// <param name="outputBytes">The bytes returned from watermarking.</param>
    /// <param name="exception">The optional exception to throw.</param>
    public TestXpsWatermarker(byte[] outputBytes, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(outputBytes);

        this.outputBytes = outputBytes;
        this.exception = exception;
    }

    /// <summary>
    /// Gets the number of watermark calls.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Gets the last source format passed to the watermarker.
    /// </summary>
    public PdlFormat? LastSourceFormat { get; private set; }

    /// <summary>
    /// Gets the last source bytes passed to the watermarker.
    /// </summary>
    public byte[] LastSourceBytes { get; private set; } = [];

    /// <summary>
    /// Gets the last watermark options passed to the watermarker.
    /// </summary>
    public WatermarkOptions? LastOptions { get; private set; }

    /// <inheritdoc />
    public async Task<Stream> ApplyAsync(
        Stream source,
        PdlFormat sourceFormat,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        CallCount++;
        LastSourceFormat = sourceFormat;
        LastOptions = options;
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        using MemoryStream captured = new();
        await source.CopyToAsync(captured, cancellationToken).ConfigureAwait(false);
        LastSourceBytes = captured.ToArray();

        if (exception is not null)
        {
            throw exception;
        }

        return new MemoryStream(outputBytes);
    }
}
