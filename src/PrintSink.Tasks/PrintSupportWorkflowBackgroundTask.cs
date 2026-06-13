using Windows.ApplicationModel.Background;
using Windows.Graphics.Printing.Workflow;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Settings;

namespace PrintSink.Tasks;

/// <summary>
/// Handles the Print Support workflow background contract for printer-parity jobs.
/// </summary>
public sealed class PrintSupportWorkflowBackgroundTask : IBackgroundTask
{
    private readonly BackgroundTaskHandlerState state = new();

    /// <inheritdoc />
    public void Run(IBackgroundTaskInstance taskInstance)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);

        state.Attach(taskInstance);

        if (taskInstance.TriggerDetails is not PrintWorkflowJobTriggerDetails jobDetails)
        {
            state.CompleteWhenIdle();
            return;
        }

        PrintWorkflowJobBackgroundSession session = jobDetails.PrintWorkflowJobSession;
        session.JobStarting += OnJobStarting;
        session.PdlModificationRequested += OnPdlModificationRequested;
        session.Start();
    }

    private void OnJobStarting(PrintWorkflowJobBackgroundSession sender, PrintWorkflowJobStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            bool compressionDisabled = false;
            state.Run(() =>
            {
                if (args.IsIppCompressionEnabled)
                {
                    args.DisableIppCompressionForJob();
                    compressionDisabled = true;
                }
            });
            string compressionDetail = compressionDisabled ? "ippCompression=disabled" : "ippCompression=unchanged";
            AppendDiagnostic("Workflow job starting", string.Empty, $"skipSystemRendering=default; {compressionDetail}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnPdlModificationRequested(
        PrintWorkflowJobBackgroundSession sender,
        PrintWorkflowPdlModificationRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            Exception? handlerException = null;
            bool succeeded = state.Run(() =>
            {
                LocalSettingsStore settingsStore = PackagedSettingsStoreFactory.Create();
                if (!CompleteJobUi(args, settingsStore))
                {
                    return;
                }

                JobProcessingOptions? jobProcessingOptions = settingsStore
                    .ConsumeJobProcessingOptionsAsync()
                    .GetAwaiter()
                    .GetResult();
                PrintWorkflowPdlSourceContent sourceContent = args.SourceContent;
                AppendDiagnostic(
                    "Workflow job passed through",
                    GetPrinterName(args),
                    FormatPassthroughDetail(sourceContent.ContentType, jobProcessingOptions));
            }, exception => handlerException = exception);

            if (!succeeded)
            {
                string detail = handlerException is null
                    ? "Background handler was already busy."
                    : handlerException.ToString();
                AppendDiagnostic("Workflow job failed", GetPrinterName(args), detail);
                args.Configuration.AbortPrintFlow(PrintWorkflowJobAbortReason.JobFailed);
            }
        }
        catch (Exception ex)
        {
            AppendDiagnostic("Workflow job failed", GetPrinterName(args), ex.ToString());
            args.Configuration.AbortPrintFlow(PrintWorkflowJobAbortReason.JobFailed);
        }
        finally
        {
            deferral.Complete();
            state.CompleteWhenIdle();
        }
    }

    private static bool CompleteJobUi(
        PrintWorkflowPdlModificationRequestedEventArgs args,
        LocalSettingsStore settingsStore)
    {
        JobUiOptions options = settingsStore
            .GetJobUiOptionsAsync()
            .GetAwaiter()
            .GetResult();
        if (!options.LaunchJobUi)
        {
            return true;
        }

        if (!args.UILauncher.IsUILaunchEnabled())
        {
            return true;
        }

        PrintWorkflowUICompletionStatus uiResult = args.UILauncher
            .LaunchAndCompleteUIAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (uiResult == PrintWorkflowUICompletionStatus.Completed)
        {
            return true;
        }

        args.Configuration.AbortPrintFlow(uiResult == PrintWorkflowUICompletionStatus.UserCanceled
            ? PrintWorkflowJobAbortReason.UserCanceled
            : PrintWorkflowJobAbortReason.JobFailed);
        return false;
    }

    private static string FormatPassthroughDetail(
        string sourceContentType,
        JobProcessingOptions? jobProcessingOptions)
    {
        string passwordStatus = jobProcessingOptions?.JobPasswordOptions is null
            ? "job-password=absent"
            : "job-password=not-applied";
        return $"source={sourceContentType}; target=system; {passwordStatus}";
    }

    private static string GetPrinterName(PrintWorkflowPdlModificationRequestedEventArgs args)
    {
        try
        {
            return args.PrinterJob.Printer.PrinterName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static void AppendDiagnostic(string message, string endpoint, string detail)
    {
        try
        {
            PackagedSettingsStoreFactory
                .CreateDiagnosticEventStore()
                .AppendAsync(
                    new DiagnosticEventRecord(
                        DateTimeOffset.UtcNow,
                        DiagnosticEventSeverity.Information,
                        nameof(PrintSupportWorkflowBackgroundTask),
                        message,
                        endpoint,
                        detail))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            // Diagnostics must not make the PSA workflow contract fail.
        }
    }
}
