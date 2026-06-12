using PrintSink.Core.Endpoints;

namespace PrintSink.Cli;

/// <summary>
/// Captures sink bytes for CLI cloud and custom fixture tests.
/// </summary>
internal sealed class CapturingSink : ISink
{
    /// <summary>
    /// Gets the number of bytes captured by the sink.
    /// </summary>
    public long BytesWritten { get; private set; }

    /// <inheritdoc />
    public async Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);

        using MemoryStream buffer = new();
        await pdl.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        BytesWritten = buffer.Length;
    }
}
