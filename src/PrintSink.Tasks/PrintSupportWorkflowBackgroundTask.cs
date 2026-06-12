using Windows.ApplicationModel.Background;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;
using PrintSink.Core.Pdl;
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
                PrinterDocumentFormatPlan plan = GetDocumentFormatPlan(args, sourceContent.ContentType);
                PrintWorkflowPdlTargetStream targetStream = CreateJobOnPrinter(args, plan.TargetContentType);
                ClearPendingJobOptions();
                SubmitPdl(args, sourceContent, targetStream, plan);

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

    private static PrinterDocumentFormatPlan GetDocumentFormatPlan(
        PrintWorkflowPdlModificationRequestedEventArgs args,
        string sourceContentType)
    {
        try
        {
            Dictionary<string, WinRtIppAttributeValue> attributes = new(
                args.PrinterJob.Printer.GetPrinterAttributes(
                    ["document-format-default", "document-format-supported"]),
                StringComparer.OrdinalIgnoreCase);

            string? defaultDocumentFormat = GetFirstKeyword(attributes, "document-format-default");
            IReadOnlyList<string> supportedDocumentFormats = GetKeywords(attributes, "document-format-supported");
            return PrinterDocumentFormatSelector.Select(
                sourceContentType,
                defaultDocumentFormat,
                supportedDocumentFormats);
        }
        catch (Exception)
        {
            return PrinterDocumentFormatSelector.Select(sourceContentType, null, []);
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

    private static void ClearPendingJobOptions()
    {
        PackagedSettingsStoreFactory
            .Create()
            .ConsumeJobProcessingOptionsAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static void SubmitPdl(
        PrintWorkflowPdlModificationRequestedEventArgs args,
        PrintWorkflowPdlSourceContent sourceContent,
        PrintWorkflowPdlTargetStream targetStream,
        PrinterDocumentFormatPlan plan)
    {
        if (plan.ConversionKind is null)
        {
            RandomAccessStream.CopyAndCloseAsync(
                sourceContent.GetInputStream(),
                targetStream.GetOutputStream()).AsTask().GetAwaiter().GetResult();
            return;
        }

        PrintWorkflowPdlConverter converter = args.GetPdlConverter(ToWinRtConversionType(plan.ConversionKind.Value));
        converter.ConvertPdlAsync(
            args.PrinterJob.GetJobPrintTicket(),
            sourceContent.GetInputStream(),
            targetStream.GetOutputStream()).AsTask().GetAwaiter().GetResult();
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

    private static string? GetFirstKeyword(
        IReadOnlyDictionary<string, WinRtIppAttributeValue> attributes,
        string attributeName)
    {
        IReadOnlyList<string> values = GetKeywords(attributes, attributeName);
        return values.Count == 0 ? null : values[0];
    }

    private static IReadOnlyList<string> GetKeywords(
        IReadOnlyDictionary<string, WinRtIppAttributeValue> attributes,
        string attributeName)
    {
        return attributes.TryGetValue(attributeName, out WinRtIppAttributeValue? value)
            ? [.. value.GetKeywordArray()]
            : [];
    }

    private static PrintWorkflowPdlConversionType ToWinRtConversionType(PdlConversionKind conversionKind)
    {
        return conversionKind switch
        {
            PdlConversionKind.XpsToPdf => PrintWorkflowPdlConversionType.XpsToPdf,
            PdlConversionKind.XpsToPwgRaster => PrintWorkflowPdlConversionType.XpsToPwgr,
            PdlConversionKind.XpsToPclm => PrintWorkflowPdlConversionType.XpsToPclm,
            _ => throw new ArgumentOutOfRangeException(nameof(conversionKind), conversionKind, "Unknown PDL conversion kind."),
        };
    }
}
