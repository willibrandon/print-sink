using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PrintSink.Tasks;

/// <summary>
/// Adapts a virtual-printer workflow activation to the core job contract.
/// </summary>
internal sealed class WinRtVirtualPrinterJob : IVirtualPrinterJob, IDisposable
{
    private static readonly TimeSpan CompleteJobTimeout = TimeSpan.FromSeconds(10);

    private readonly PrintWorkflowVirtualPrinterDataAvailableEventArgs args;
    private readonly WorkflowPrintTicket printTicket;
    private StorageFile? targetFile;
    private MemoryStream? targetBuffer;
    private bool completed;

    internal WinRtVirtualPrinterJob(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args,
        VirtualEndpoint endpoint,
        WorkflowPrintTicket printTicket)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(printTicket);

        this.args = args;
        this.printTicket = printTicket;
        Endpoint = endpoint;
        ContentType = args.SourceContent.ContentType;
    }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public VirtualEndpoint Endpoint { get; }

    /// <inheritdoc />
    public async ValueTask<Stream> OpenSourceAsync(CancellationToken cancellationToken = default)
    {
        using IInputStream input = args.SourceContent.GetInputStream();
        return await WinRtStreamBridge.ReadToMemoryAsync(input, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Stream?> OpenTargetAsync(CancellationToken cancellationToken = default)
    {
        if (!Endpoint.RequiresTargetFile)
        {
            return null;
        }

        targetFile = await args.GetTargetFileAsync().AsTask(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Endpoint '{Endpoint.QueueName}' requires a target file.");

        targetBuffer = new MemoryStream();
        return targetBuffer;
    }

    /// <inheritdoc />
    public ValueTask<IPrintTicket> GetPrintTicketAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IPrintTicket>(new WinRtPrintTicket(printTicket));
    }

    /// <inheritdoc />
    public async Task CompleteAsync(
        VirtualPrinterJobStatus status,
        CancellationToken cancellationToken = default)
    {
        if (completed)
        {
            return;
        }

        try
        {
            if (status == VirtualPrinterJobStatus.Succeeded && targetFile is not null && targetBuffer is not null)
            {
                targetBuffer.Position = 0;
                await FileIO
                    .WriteBytesAsync(targetFile, targetBuffer.ToArray())
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);
            }

            await CompleteSubmittedJobAsync(status, cancellationToken).ConfigureAwait(false);
            completed = true;
        }
        finally
        {
            MemoryStream? buffer = targetBuffer;
            targetBuffer = null;
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        targetBuffer?.Dispose();
        targetBuffer = null;
    }

    private static PrintWorkflowSubmittedStatus ToWinRtStatus(VirtualPrinterJobStatus status)
    {
        return status switch
        {
            VirtualPrinterJobStatus.Succeeded => PrintWorkflowSubmittedStatus.Succeeded,
            VirtualPrinterJobStatus.Canceled => PrintWorkflowSubmittedStatus.Canceled,
            VirtualPrinterJobStatus.Failed => PrintWorkflowSubmittedStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown virtual printer job status."),
        };
    }

    private async Task CompleteSubmittedJobAsync(
        VirtualPrinterJobStatus status,
        CancellationToken cancellationToken)
    {
        Task completeTask = Task.Run(() => args.CompleteJob(ToWinRtStatus(status)), CancellationToken.None);
        _ = completeTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            await completeTask.WaitAsync(CompleteJobTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Some passthrough jobs finish writing output but do not return from CompleteJob promptly.
        }
    }
}
