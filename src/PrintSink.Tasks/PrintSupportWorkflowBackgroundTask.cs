using Windows.ApplicationModel.Background;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;
using PrintSink.Core.Pdl;
using PrintSink.Core.Settings;
using PrintSink.Core.Tickets;
using Windows.Security.Cryptography;
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
                if (!CompleteJobUi(args))
                {
                    return;
                }

                LocalSettingsStore settingsStore = PackagedSettingsStoreFactory.Create();
                JobProcessingOptions? jobProcessingOptions = settingsStore
                    .ConsumeJobProcessingOptionsAsync()
                    .GetAwaiter()
                    .GetResult();
                PrintWorkflowPdlSourceContent sourceContent = args.SourceContent;
                PrinterDocumentFormatPlan plan = GetDocumentFormatPlan(args, sourceContent.ContentType);
                PrintWorkflowPdlTargetStream targetStream = CreateJobOnPrinter(args, plan.TargetContentType, jobProcessingOptions);
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

    private static bool CompleteJobUi(PrintWorkflowPdlModificationRequestedEventArgs args)
    {
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
        string documentFormat,
        JobProcessingOptions? jobProcessingOptions)
    {
        IDictionary<string, WinRtIppAttributeValue> jobAttributes = args.PrinterJob.ConvertPrintTicketToJobAttributes(
            args.PrinterJob.GetJobPrintTicket(),
            documentFormat);
        IDictionary<string, WinRtIppAttributeValue> filteredAttributes = ApplyMergePolicy(
            jobAttributes,
            AttributeMergePolicyOptions.RemovePdlEmbeddedMediaSize);
        Dictionary<string, WinRtIppAttributeValue> operationAttributes = BuildOperationAttributes(jobProcessingOptions);

        return args.CreateJobOnPrinterWithAttributes(
            filteredAttributes,
            documentFormat,
            operationAttributes,
            PrintWorkflowAttributesMergePolicy.DoNotMergeWithPrintTicket,
            PrintWorkflowAttributesMergePolicy.MergePreferPrintTicketOnConflict);
    }

    private static Dictionary<string, WinRtIppAttributeValue> BuildOperationAttributes(
        JobProcessingOptions? jobProcessingOptions)
    {
        Dictionary<string, WinRtIppAttributeValue> operationAttributes = new(StringComparer.OrdinalIgnoreCase);
        JobPasswordOptions? passwordOptions = jobProcessingOptions?.JobPasswordOptions;
        if (passwordOptions is null)
        {
            return operationAttributes;
        }

        Dictionary<string, WinRtIppAttributeValue> passwordCollection = new(StringComparer.OrdinalIgnoreCase)
        {
            ["job-password"] = WinRtIppAttributeValue.CreateOctetString(
                CryptographicBuffer.CreateFromByteArray(passwordOptions.GetEncryptedPassword())),
            ["job-password-encryption"] = WinRtIppAttributeValue.CreateKeyword(passwordOptions.EncryptionMethod),
        };

        operationAttributes["msft-operation-attribute-col"] = WinRtIppAttributeValue.CreateCollection(passwordCollection);
        return operationAttributes;
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

    private static Dictionary<string, WinRtIppAttributeValue> ApplyMergePolicy(
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
        Dictionary<string, WinRtIppAttributeValue> attributes,
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
