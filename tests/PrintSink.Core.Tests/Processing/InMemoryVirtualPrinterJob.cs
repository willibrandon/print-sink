using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Provides an in-memory virtual printer job fixture.
/// </summary>
internal sealed class InMemoryVirtualPrinterJob : IVirtualPrinterJob
{
    private readonly byte[] source;
    private readonly MemoryStream? target;

    internal InMemoryVirtualPrinterJob(
        string contentType,
        VirtualEndpoint endpoint,
        byte[] source,
        bool hasTarget)
    {
        ContentType = contentType;
        Endpoint = endpoint;
        this.source = source;
        target = hasTarget ? new MemoryStream() : null;
    }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public VirtualEndpoint Endpoint { get; }

    internal VirtualPrinterJobStatus? CompletedStatus { get; private set; }

    internal byte[] TargetBytes => target?.ToArray() ?? [];

    /// <inheritdoc />
    public ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Stream>(new MemoryStream(source));
    }

    /// <inheritdoc />
    public ValueTask<Stream?> OpenTargetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Stream?>(target);
    }

    /// <inheritdoc />
    public ValueTask<IPrintTicket> GetPrintTicketAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<IPrintTicket>(new InMemoryPrintTicket("<PrintTicket />"));
    }

    /// <inheritdoc />
    public Task CompleteAsync(VirtualPrinterJobStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (status == VirtualPrinterJobStatus.Succeeded && target is not null)
        {
            target.Position = 0;
        }

        CompletedStatus = status;
        return Task.CompletedTask;
    }
}
