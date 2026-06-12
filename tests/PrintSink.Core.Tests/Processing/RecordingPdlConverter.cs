using System.Text;
using PrintSink.Pdl;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Records PDL conversion calls for processor tests.
/// </summary>
public sealed class RecordingPdlConverter : IPdlConverter
{
    /// <summary>
    /// Gets the requested conversion.
    /// </summary>
    public PdlConversionKind Conversion { get; private set; }

    /// <summary>
    /// Gets the print ticket XML provided for conversion.
    /// </summary>
    public string? PrintTicketXml { get; private set; }

    /// <summary>
    /// Gets the source bytes observed by the converter.
    /// </summary>
    public byte[] SourceBytes { get; private set; } = Array.Empty<byte>();

    /// <inheritdoc />
    public async Task ConvertAsync(
        PdlConversionKind conversion,
        string printTicketXml,
        Stream source,
        Stream target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printTicketXml);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        Conversion = conversion;
        PrintTicketXml = printTicketXml;

        using MemoryStream buffer = new();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        SourceBytes = buffer.ToArray();

        byte[] output = Encoding.UTF8.GetBytes("converted:" + conversion);
        await target.WriteAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
