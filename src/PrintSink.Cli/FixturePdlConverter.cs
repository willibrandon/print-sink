using System.Text;
using PrintSink.Core.Pdl;

namespace PrintSink.Cli;

/// <summary>
/// Provides deterministic conversion output for CLI fixture processing.
/// </summary>
internal sealed class FixturePdlConverter : IPdlConverter
{
    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(
        Stream source,
        PdlConversionKind conversionKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        MemoryStream output = new();
        byte[] header = Encoding.UTF8.GetBytes($"PrintSink fixture conversion: {conversionKind}\n");
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        output.Position = 0;

        return output;
    }
}
