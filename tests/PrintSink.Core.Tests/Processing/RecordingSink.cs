using PrintSink.Endpoints;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Records sink writes for processor tests.
/// </summary>
internal sealed class RecordingSink : ISink
{
    /// <summary>
    /// Gets the written bytes.
    /// </summary>
    public byte[] Bytes { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the write context.
    /// </summary>
    public SinkWriteContext? Context { get; private set; }

    /// <inheritdoc />
    public async Task WriteAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdlStream);
        ArgumentNullException.ThrowIfNull(context);

        using MemoryStream buffer = new();
        await pdlStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        Bytes = buffer.ToArray();
        Context = context;
    }
}
