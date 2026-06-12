namespace PrintSink.Abstractions;

using PrintSink.Endpoints;

/// <summary>
/// Describes the data needed to route one virtual printer job.
/// </summary>
public interface IVirtualPrinterJob
{
    /// <summary>
    /// Gets the endpoint selected by the printer queue.
    /// </summary>
    VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Gets the content type reported by the print workflow source.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Opens the source PDL stream.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels opening the stream.</param>
    /// <returns>The source PDL stream.</returns>
    ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken);
}
