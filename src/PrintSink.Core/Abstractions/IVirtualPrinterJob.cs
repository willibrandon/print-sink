using PrintSink.Core.Endpoints;

namespace PrintSink.Core.Abstractions;

/// <summary>
/// Describes a virtual printer job without depending on live print workflow event objects.
/// </summary>
public interface IVirtualPrinterJob
{
    /// <summary>
    /// Gets the source stream content type.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the endpoint that should receive the job.
    /// </summary>
    VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Opens the source PDL stream.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The source PDL stream.</returns>
    ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the target stream when the endpoint uses a file target.
    /// </summary>
    /// <remarks>
    /// The job owns the returned stream lifetime because some adapters commit the stream during
    /// <see cref="CompleteAsync(VirtualPrinterJobStatus, CancellationToken)"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The target stream, or <see langword="null"/> for non-file sinks.</returns>
    ValueTask<Stream?> OpenTargetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the job print ticket.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The print ticket.</returns>
    ValueTask<IPrintTicket> GetPrintTicketAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the job with the supplied status.
    /// </summary>
    /// <param name="status">The final job status.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the job is marked complete.</returns>
    Task CompleteAsync(VirtualPrinterJobStatus status, CancellationToken cancellationToken = default);
}
