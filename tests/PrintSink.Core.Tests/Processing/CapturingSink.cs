using PrintSink.Core.Endpoints;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Captures bytes written through the sink contract.
/// </summary>
internal sealed class CapturingSink : ISink
{
    private readonly Exception? exception;

    internal CapturingSink(Exception? exception = null)
    {
        this.exception = exception;
    }

    internal byte[] Bytes { get; private set; } = [];

    internal SinkWriteContext? Context { get; private set; }

    /// <inheritdoc />
    public async Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);

        if (exception is not null)
        {
            throw exception;
        }

        using MemoryStream buffer = new();
        await pdl.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        Bytes = buffer.ToArray();
        Context = context;
    }
}
