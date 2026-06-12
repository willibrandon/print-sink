using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Provides a deterministic PDL transformer fixture.
/// </summary>
internal sealed class TestPdlTransformer : IPdlTransformer
{
    private readonly byte[] transformedBytes;
    private readonly Exception? exception;

    internal TestPdlTransformer(byte[] transformedBytes, Exception? exception = null)
    {
        this.transformedBytes = transformedBytes;
        this.exception = exception;
    }

    internal int CallCount { get; private set; }

    internal byte[] LastSourceBytes { get; private set; } = [];

    internal WatermarkOptions? LastWatermarkOptions { get; private set; }

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
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastWatermarkOptions = watermarkOptions;
        using MemoryStream sourceBuffer = new();
        await source.CopyToAsync(sourceBuffer, cancellationToken).ConfigureAwait(false);
        LastSourceBytes = sourceBuffer.ToArray();

        if (exception is not null)
        {
            throw exception;
        }

        return new MemoryStream(transformedBytes);
    }
}
