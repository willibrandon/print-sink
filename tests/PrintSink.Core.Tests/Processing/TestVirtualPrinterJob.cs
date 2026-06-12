using PrintSink.Abstractions;
using PrintSink.Endpoints;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Provides an in-memory virtual printer job for processor tests.
/// </summary>
public sealed class TestVirtualPrinterJob : IVirtualPrinterJob
{
    private readonly byte[] sourceBytes;
    private readonly ISink sink;
    private readonly string printTicketXml;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestVirtualPrinterJob"/> class.
    /// </summary>
    /// <param name="contentType">The source content type.</param>
    /// <param name="endpoint">The virtual endpoint.</param>
    /// <param name="sourceBytes">The source bytes.</param>
    /// <param name="sink">The sink to open.</param>
    /// <param name="printTicketXml">The print ticket XML.</param>
    /// <param name="jobName">The job name.</param>
    public TestVirtualPrinterJob(
        string contentType,
        VirtualEndpoint endpoint,
        byte[] sourceBytes,
        ISink sink,
        string printTicketXml,
        string? jobName = "processor-test")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(sourceBytes);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentException.ThrowIfNullOrWhiteSpace(printTicketXml);

        ContentType = contentType;
        Endpoint = endpoint;
        this.sourceBytes = sourceBytes;
        this.sink = sink;
        this.printTicketXml = printTicketXml;
        JobName = jobName;
    }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public VirtualEndpoint Endpoint { get; }

    /// <inheritdoc />
    public string? JobName { get; }

    /// <summary>
    /// Gets a value indicating whether the source stream was opened.
    /// </summary>
    public bool WasSourceOpened { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the sink was opened.
    /// </summary>
    public bool WasSinkOpened { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the print ticket was read.
    /// </summary>
    public bool WasPrintTicketRead { get; private set; }

    /// <inheritdoc />
    public ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WasSourceOpened = true;
        return ValueTask.FromResult<Stream>(new MemoryStream(sourceBytes, writable: false));
    }

    /// <inheritdoc />
    public ValueTask<ISink> OpenSinkAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WasSinkOpened = true;
        return ValueTask.FromResult(sink);
    }

    /// <inheritdoc />
    public ValueTask<string> GetPrintTicketXmlAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WasPrintTicketRead = true;
        return ValueTask.FromResult(printTicketXml);
    }
}
