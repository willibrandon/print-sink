using System.Text;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli;

/// <summary>
/// Adapts CLI fixture files to the virtual printer job abstraction.
/// </summary>
internal sealed class FixtureVirtualPrinterJob : IVirtualPrinterJob
{
    private readonly byte[] defaultSource;
    private readonly string? inputPath;
    private readonly string? outputPath;
    private Stream? targetStream;
    private string? temporaryOutputPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixtureVirtualPrinterJob"/> class.
    /// </summary>
    /// <param name="contentType">The source content type.</param>
    /// <param name="endpoint">The endpoint that receives the job.</param>
    /// <param name="inputPath">The optional source fixture path.</param>
    /// <param name="outputPath">The optional target fixture path.</param>
    public FixtureVirtualPrinterJob(
        string contentType,
        VirtualEndpoint endpoint,
        string? inputPath,
        string? outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(endpoint);

        ContentType = contentType;
        Endpoint = endpoint;
        this.inputPath = inputPath;
        this.outputPath = outputPath;
        defaultSource = Encoding.UTF8.GetBytes("%PrintSink fixture%");
    }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Gets the final status passed to <see cref="CompleteAsync"/>.
    /// </summary>
    public VirtualPrinterJobStatus? CompletedStatus { get; private set; }

    /// <summary>
    /// Gets the number of bytes written to the target.
    /// </summary>
    public long OutputBytes
    {
        get
        {
            string? path = outputPath ?? temporaryOutputPath;
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? new FileInfo(path).Length
                : 0;
        }
    }

    /// <inheritdoc />
    public ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Stream source = string.IsNullOrWhiteSpace(inputPath)
            ? new MemoryStream(defaultSource)
            : File.OpenRead(inputPath);

        return ValueTask.FromResult(source);
    }

    /// <inheritdoc />
    public ValueTask<Stream?> OpenTargetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Endpoint.RequiresTargetFile)
        {
            return ValueTask.FromResult<Stream?>(null);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            string fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            targetStream = File.Create(fullPath);
            return ValueTask.FromResult<Stream?>(targetStream);
        }

        temporaryOutputPath = Path.GetTempFileName();
        targetStream = File.Create(temporaryOutputPath);
        return ValueTask.FromResult<Stream?>(targetStream);
    }

    /// <inheritdoc />
    public ValueTask<IPrintTicket> GetPrintTicketAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<IPrintTicket>(new FixturePrintTicket());
    }

    /// <inheritdoc />
    public Task CompleteAsync(VirtualPrinterJobStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CompletedStatus = status;
        targetStream?.Dispose();
        targetStream = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes a temporary target file created for an in-memory fixture run.
    /// </summary>
    public void DeleteTemporaryOutput()
    {
        if (!string.IsNullOrWhiteSpace(temporaryOutputPath) && File.Exists(temporaryOutputPath))
        {
            File.Delete(temporaryOutputPath);
        }
    }
}
