using Windows.ApplicationModel.Background;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Pdl;
using PrintSink.Core.Settings;
using PrintSink.Core.Tickets;
using Windows.Security.Cryptography;
using CoreIppAttributeValue = PrintSink.Core.Tickets.IppAttributeValue;
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
            AppendDiagnostic("Workflow job starting", string.Empty, "skipSystemRendering=set");
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
                PrinterDocumentFormatPlan plan = GetDocumentFormatPlan(args, sourceContent.ContentType);
                AppendDiagnostic(
                    "Workflow route resolved",
                    GetPrinterName(args),
                    FormatRouteDetail(plan));
                PrintWorkflowPdlTargetStream targetStream = CreateJobOnPrinter(args, plan.TargetContentType, jobProcessingOptions);
                SubmitPdl(args, sourceContent, targetStream, plan);

                targetStream.CompleteStreamSubmission(PrintWorkflowSubmittedStatus.Succeeded);
                AppendDiagnostic("Workflow job completed", GetPrinterName(args), $"target={plan.TargetContentType}");
            });

            if (!succeeded)
            {
                AppendDiagnostic("Workflow job failed", GetPrinterName(args), "Background handler was already busy.");
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

    private static PrinterDocumentFormatPlan GetDocumentFormatPlan(
        PrintWorkflowPdlModificationRequestedEventArgs args,
        string sourceContentType)
    {
        return PrinterDocumentFormatSelector.Select(sourceContentType, ReadDocumentFormatAttributes(args));
    }

    private static IppAttributeReadResult ReadDocumentFormatAttributes(
        PrintWorkflowPdlModificationRequestedEventArgs args)
    {
        try
        {
            Dictionary<string, WinRtIppAttributeValue> attributes = new(
                args.PrinterJob.Printer.GetPrinterAttributes(
                    ["document-format-default", "document-format-supported"]),
                StringComparer.OrdinalIgnoreCase);

            return IppAttributeReadResult.Success(ToCoreAttributes(attributes));
        }
        catch (Exception ex) when (IsUnsupportedAttributeRead(ex))
        {
            return IppAttributeReadResult.NotSupported(ex.Message);
        }
        catch (Exception ex)
        {
            return IppAttributeReadResult.Failed(ex.Message);
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
        AppendDiagnostic(
            "Workflow job attributes prepared",
            GetPrinterName(args),
            FormatAttributePreparationDetail(filteredAttributes, operationAttributes));

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

    private static string FormatAttributePreparationDetail(
        IDictionary<string, WinRtIppAttributeValue> jobAttributes,
        IDictionary<string, WinRtIppAttributeValue> operationAttributes)
    {
        string jobAttributeNames = string.Join(
            ',',
            jobAttributes.Keys.Order(StringComparer.OrdinalIgnoreCase));
        string operationAttributeNames = string.Join(
            ',',
            operationAttributes.Keys.Order(StringComparer.OrdinalIgnoreCase));
        string passwordStatus = operationAttributes.ContainsKey("msft-operation-attribute-col")
            ? "job-password=present; job-password-encryption=present"
            : "job-password=absent";

        return $"jobAttributes={jobAttributeNames}; operationAttributes={operationAttributeNames}; {passwordStatus}; mergePolicy=RemovePdlEmbeddedMediaSize";
    }

    private static string FormatRouteDetail(PrinterDocumentFormatPlan plan)
    {
        string action = plan.ConversionKind is null ? "Copy" : "Convert";
        string conversion = plan.ConversionKind?.ToString() ?? "none";
        return $"{plan.SourceContentType} -> {plan.TargetContentType}; {action}; conversion={conversion}";
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

    private static Dictionary<string, CoreIppAttributeValue> ToCoreAttributes(
        IReadOnlyDictionary<string, WinRtIppAttributeValue> attributes)
    {
        Dictionary<string, CoreIppAttributeValue> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, WinRtIppAttributeValue> attribute in attributes)
        {
            IReadOnlyList<string> values = [.. attribute.Value.GetKeywordArray()];
            if (values.Count > 0)
            {
                result[attribute.Key] = new CoreIppAttributeValue(attribute.Key, values);
            }
        }

        return result;
    }

    private static bool IsUnsupportedAttributeRead(Exception ex)
    {
        const int ErrorNotSupported = unchecked((int)0x80070032);

        return ex is NotSupportedException || ex.HResult == ErrorNotSupported;
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
