using Windows.ApplicationModel.Background;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;
using PrintSink.Core.Tickets;
using WinRtIppAttributeValue = Windows.Devices.Printers.IppAttributeValue;

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
            state.Run(args.SetSkipSystemRendering);
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
            bool succeeded = state.Run(() =>
            {
                PrintWorkflowPdlSourceContent sourceContent = args.SourceContent;
                PrintWorkflowPdlTargetStream targetStream = CreateJobOnPrinter(args, sourceContent.ContentType);
                RandomAccessStream.CopyAndCloseAsync(
                    sourceContent.GetInputStream(),
                    targetStream.GetOutputStream()).AsTask().GetAwaiter().GetResult();

                targetStream.CompleteStreamSubmission(PrintWorkflowSubmittedStatus.Succeeded);
            });

            if (!succeeded)
            {
                args.Configuration.AbortPrintFlow(PrintWorkflowJobAbortReason.JobFailed);
            }
        }
        catch (Exception)
        {
            args.Configuration.AbortPrintFlow(PrintWorkflowJobAbortReason.JobFailed);
        }
        finally
        {
            deferral.Complete();
            state.CompleteWhenIdle();
        }
    }

    private static PrintWorkflowPdlTargetStream CreateJobOnPrinter(
        PrintWorkflowPdlModificationRequestedEventArgs args,
        string documentFormat)
    {
        IDictionary<string, WinRtIppAttributeValue> jobAttributes = args.PrinterJob.ConvertPrintTicketToJobAttributes(
            args.PrinterJob.GetJobPrintTicket(),
            documentFormat);
        IDictionary<string, WinRtIppAttributeValue> filteredAttributes = ApplyMergePolicy(
            jobAttributes,
            AttributeMergePolicyOptions.RemovePdlEmbeddedMediaSize);
        Dictionary<string, WinRtIppAttributeValue> operationAttributes = new(StringComparer.OrdinalIgnoreCase);

        return args.CreateJobOnPrinterWithAttributes(
            filteredAttributes,
            documentFormat,
            operationAttributes,
            PrintWorkflowAttributesMergePolicy.DoNotMergeWithPrintTicket,
            PrintWorkflowAttributesMergePolicy.MergePreferPrintTicketOnConflict);
    }

    private static IDictionary<string, WinRtIppAttributeValue> ApplyMergePolicy(
        IDictionary<string, WinRtIppAttributeValue> attributes,
        AttributeMergePolicyOptions options)
    {
        Dictionary<string, WinRtIppAttributeValue> result = new(attributes, StringComparer.OrdinalIgnoreCase);
        foreach (string attributeName in options.AttributesToRemove)
        {
            result.Remove(attributeName);
        }

        foreach (IppCollectionMemberRemoval removal in options.CollectionMemberRemovals)
        {
            RemoveCollectionMember(result, removal);
        }

        return result;
    }

    private static void RemoveCollectionMember(
        IDictionary<string, WinRtIppAttributeValue> attributes,
        IppCollectionMemberRemoval removal)
    {
        if (!attributes.TryGetValue(removal.AttributeName, out WinRtIppAttributeValue? attribute))
        {
            return;
        }

        IList<IReadOnlyDictionary<string, WinRtIppAttributeValue>> collections = attribute.GetCollectionArray();
        if (collections.Count == 0)
        {
            return;
        }

        Dictionary<string, WinRtIppAttributeValue> updatedCollection = new(
            collections[0],
            StringComparer.OrdinalIgnoreCase);
        if (updatedCollection.Remove(removal.MemberName))
        {
            attributes[removal.AttributeName] = WinRtIppAttributeValue.CreateCollection(updatedCollection);
        }
    }
}
