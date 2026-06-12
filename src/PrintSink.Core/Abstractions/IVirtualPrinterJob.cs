using PrintSink.Endpoints;

namespace PrintSink.Abstractions;

/// <summary>
/// Provides print-stack-neutral access to a virtual printer job.
/// </summary>
public interface IVirtualPrinterJob
{
    /// <summary>
    /// Gets the source content type reported by the print workflow.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the virtual endpoint for the selected print queue.
    /// </summary>
    VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Gets the print job name when available.
    /// </summary>
    string? JobName { get; }

    /// <summary>
    /// Opens the source PDL stream.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The source PDL stream positioned at the beginning.</returns>
    ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the target sink for the job.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The sink that will receive transformed PDL content.</returns>
    ValueTask<ISink> OpenSinkAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current print ticket as XML.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The print ticket XML.</returns>
    ValueTask<string> GetPrintTicketXmlAsync(CancellationToken cancellationToken = default);
}
