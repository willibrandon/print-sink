using Windows.ApplicationModel.Background;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage;
using Windows.Storage.Streams;

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
        PrintWorkflowSubmittedStatus status = PrintWorkflowSubmittedStatus.Failed;
        try
        {
            state.Run(() =>
            {
                StorageFile? targetFile = args.GetTargetFileAsync().AsTask().GetAwaiter().GetResult();
                if (targetFile is null)
                {
                    status = PrintWorkflowSubmittedStatus.Succeeded;
                    return;
                }

                using IRandomAccessStream targetStream = targetFile.OpenAsync(FileAccessMode.ReadWrite)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                using IOutputStream output = targetStream.GetOutputStreamAt(0);

                PrintWorkflowPdlSourceContent sourceContent = args.SourceContent;
                IInputStream input = sourceContent.GetInputStream();
                PrintWorkflowPdlConversionType? conversionType = ResolveConversion(sourceContent.ContentType, targetFile.FileType);

                if (conversionType is null)
                {
                    RandomAccessStream.CopyAndCloseAsync(input, output).AsTask().GetAwaiter().GetResult();
                }
                else
                {
                    PrintWorkflowPdlConverter converter = args.GetPdlConverter(conversionType.Value);
                    converter.ConvertPdlAsync(args.GetJobPrintTicket(), input, output).AsTask().GetAwaiter().GetResult();
                }

                status = PrintWorkflowSubmittedStatus.Succeeded;
            });
        }
        finally
        {
            args.CompleteJob(status);
            state.CompleteWhenIdle();
        }
    }

    private static PrintWorkflowPdlConversionType? ResolveConversion(string contentType, string fileType)
    {
        if (!string.Equals(contentType, "application/oxps", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fileType.ToUpperInvariant() switch
        {
            ".PDF" => PrintWorkflowPdlConversionType.XpsToPdf,
            ".PWG" => PrintWorkflowPdlConversionType.XpsToPwgr,
            ".PCLM" => PrintWorkflowPdlConversionType.XpsToPclm,
            _ => null,
        };
    }
}
