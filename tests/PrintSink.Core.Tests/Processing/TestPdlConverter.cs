using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Provides a deterministic PDL converter fixture.
/// </summary>
internal sealed class TestPdlConverter : IPdlConverter
{
    private readonly byte[] convertedBytes;

    internal TestPdlConverter(byte[] convertedBytes)
    {
        this.convertedBytes = convertedBytes;
    }

    internal int CallCount { get; private set; }

    internal PdlConversionKind? LastConversionKind { get; private set; }

    /// <inheritdoc />
    public Task<Stream> ConvertAsync(
        Stream source,
        PdlConversionKind conversionKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastConversionKind = conversionKind;

        return Task.FromResult<Stream>(new MemoryStream(convertedBytes));
    }
}
