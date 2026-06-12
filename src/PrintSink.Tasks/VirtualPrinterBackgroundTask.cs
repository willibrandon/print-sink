using Windows.ApplicationModel.Background;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;
using PrintSink.Core.Settings;
using Windows.Graphics.Printing.Workflow;

namespace PrintSink.Tasks;

/// <summary>
/// Handles virtual printer jobs submitted to PrintSink queues.
/// </summary>
public sealed class VirtualPrinterBackgroundTask : IBackgroundTask
{
    private readonly BackgroundTaskHandlerState state = new();

    /// <inheritdoc />
    public void Run(IBackgroundTaskInstance taskInstance)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);

        state.Attach(taskInstance);

        if (taskInstance.TriggerDetails is not PrintWorkflowVirtualPrinterTriggerDetails virtualPrinterDetails)
        {
            state.CompleteWhenIdle();
            return;
        }

        PrintWorkflowVirtualPrinterSession session = virtualPrinterDetails.VirtualPrinterSession;
        session.VirtualPrinterDataAvailable += OnVirtualPrinterDataAvailable;
        session.Start();
    }

    private void OnVirtualPrinterDataAvailable(
        PrintWorkflowVirtualPrinterSession sender,
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args)
    {
        try
        {
            bool handled = state.Run(() =>
            {
                ProcessJobAsync(sender, args).GetAwaiter().GetResult();
            });

            if (!handled)
            {
                args.CompleteJob(PrintWorkflowSubmittedStatus.Failed);
            }
        }
        finally
        {
            state.CompleteWhenIdle();
        }
    }

    private static async Task ProcessJobAsync(
        PrintWorkflowVirtualPrinterSession session,
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args)
    {
        if (!EndpointCatalog.TryResolve(session.Printer.PrinterUri, out VirtualEndpoint? endpoint) || endpoint is null)
        {
            args.CompleteJob(PrintWorkflowSubmittedStatus.Failed);
            return;
        }

        LocalSettingsStore settingsStore = PackagedSettingsStoreFactory.Create();
        JobUiCompletionResult uiCompletion = await CompleteJobUiAsync(args, settingsStore).ConfigureAwait(false);
        if (!uiCompletion.ShouldProcess)
        {
            return;
        }

        JobProcessingOptions? jobProcessingOptions = uiCompletion.UsedForegroundUi
            ? await settingsStore.ConsumeJobProcessingOptionsAsync().ConfigureAwait(false)
            : null;
        Windows.Graphics.Printing.PrintTicket.WorkflowPrintTicket printTicket = args.GetJobPrintTicket();
        VirtualPrinterJobProcessor processor = CreateProcessor(args, printTicket, settingsStore, jobProcessingOptions);
        WinRtVirtualPrinterJob job = new(args, endpoint, printTicket);
        await processor.ProcessAsync(job).ConfigureAwait(false);
    }

    private static async Task<JobUiCompletionResult> CompleteJobUiAsync(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args,
        LocalSettingsStore settingsStore)
    {
        if (!args.UILauncher.IsUILaunchEnabled())
        {
            return new JobUiCompletionResult(true, false);
        }

        PrintWorkflowUICompletionStatus uiStatus = await args.UILauncher.LaunchAndCompleteUIAsync()
            .AsTask()
            .ConfigureAwait(false);
        if (uiStatus == PrintWorkflowUICompletionStatus.Completed)
        {
            return new JobUiCompletionResult(true, true);
        }

        await settingsStore.ConsumeJobProcessingOptionsAsync().ConfigureAwait(false);
        args.CompleteJob(uiStatus == PrintWorkflowUICompletionStatus.UserCanceled
            ? PrintWorkflowSubmittedStatus.Canceled
            : PrintWorkflowSubmittedStatus.Failed);
        return new JobUiCompletionResult(false, true);
    }

    private static VirtualPrinterJobProcessor CreateProcessor(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args,
        Windows.Graphics.Printing.PrintTicket.WorkflowPrintTicket printTicket,
        LocalSettingsStore settingsStore,
        JobProcessingOptions? jobProcessingOptions)
    {
        EndpointSinkResolver sinkResolver = new(new Dictionary<EndpointKind, ISink>
        {
            [EndpointKind.Pdf] = new TargetStreamSink(),
            [EndpointKind.Xps] = new TargetStreamSink(),
            [EndpointKind.PostScript] = new TargetStreamSink(),
            [EndpointKind.Cloud] = new CloudSink(DrainCloudSinkAsync),
            [EndpointKind.PwgRaster] = new TargetStreamSink(),
        });

        return new VirtualPrinterJobProcessor(
            new PdlRouter(),
            new WinRtPdlConverter(args, printTicket),
            sinkResolver,
            settingsStore,
            jobProcessingOptions);
    }

    private static async Task DrainCloudSinkAsync(
        Stream pdl,
        SinkWriteContext context,
        CancellationToken cancellationToken)
    {
        await pdl.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct JobUiCompletionResult(bool ShouldProcess, bool UsedForegroundUi);
}
