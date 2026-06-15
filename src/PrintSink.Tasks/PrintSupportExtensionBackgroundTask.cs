using System.Text.Json;
using System.Xml.Linq;
using PrintSink.Core.Capabilities;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Tickets;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources.Core;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Metadata;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Graphics.Printing.PrintSupport;
using XmlException = System.Xml.XmlException;

namespace PrintSink.Tasks;

/// <summary>
/// Handles the shared Print Support extension background contract.
/// </summary>
public sealed class PrintSupportExtensionBackgroundTask : IBackgroundTask
{
    private const string PrintSupportExtensionSessionType =
        "Windows.Graphics.Printing.PrintSupport.PrintSupportExtensionSession";
    private const string PrintSupportCapabilitiesChangedEventArgsType =
        "Windows.Graphics.Printing.PrintSupport.PrintSupportPrintDeviceCapabilitiesChangedEventArgs";
    private const string PrintSinkFeatureResourceSubtree = "PrintSinkFeatures";
    private static readonly TimeSpan AttributeCommunicationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JobCommunicationTimeout = TimeSpan.FromSeconds(120);

    private static readonly PrintDeviceCapabilitiesEditor CapabilitiesEditor = new();
    private static readonly IReadOnlyList<PrintSchemaQualifiedName> CustomResourceNames = BuildCustomResourceNames();

    private readonly BackgroundTaskHandlerState state = new();

    /// <inheritdoc />
    public void Run(IBackgroundTaskInstance taskInstance)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);

        state.Attach(taskInstance);

        if (taskInstance.TriggerDetails is not PrintSupportExtensionTriggerDetails extensionDetails)
        {
            state.CompleteWhenIdle();
            return;
        }

        PrintSupportExtensionSession session = extensionDetails.Session;
        string printerName = GetPrinterName(session);
        bool hasPrinterSelected = ApiInformation.IsEventPresent(PrintSupportExtensionSessionType, "PrinterSelected");
        bool hasCommunicationErrorDetected = ApiInformation.IsEventPresent(PrintSupportExtensionSessionType, "CommunicationErrorDetected");
        AppendDiagnostic(
            "Extension session starting",
            printerName,
            string.Join(
                "; ",
                "handlers=PrintTicketValidationRequested,PrintDeviceCapabilitiesChanged",
                $"printerSelected={(hasPrinterSelected ? "registered" : "unavailable")}",
                $"communicationErrorDetected={(hasCommunicationErrorDetected ? "registered" : "unavailable")}"));
        session.PrintTicketValidationRequested += OnPrintTicketValidationRequested;
        session.PrintDeviceCapabilitiesChanged += OnPrintDeviceCapabilitiesChanged;

        if (hasPrinterSelected)
        {
            session.PrinterSelected += OnPrinterSelected;
        }

        if (hasCommunicationErrorDetected)
        {
            session.CommunicationErrorDetected += OnCommunicationErrorDetected;
        }

        session.Start();
    }

    private void OnPrintTicketValidationRequested(
        PrintSupportExtensionSession sender,
        PrintSupportPrintTicketValidationRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            state.Run(() =>
            {
                WorkflowPrintTicketValidationStatus status = ValidatePrintTicket(args.PrintTicket);
                args.SetPrintTicketValidationStatus(status);
                AppendDiagnostic(
                    "Print ticket validated",
                    sender.Printer.PrinterName,
                    $"status={status}");
            });
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static WorkflowPrintTicketValidationStatus ValidatePrintTicket(WorkflowPrintTicket printTicket)
    {
        ArgumentNullException.ThrowIfNull(printTicket);

        try
        {
            XDocument document = XDocument.Parse(printTicket.XmlNode.GetXml(), LoadOptions.PreserveWhitespace);
            IReadOnlyList<string> messages = PrintTicketValidator.Validate(document);
            return messages.Count == 0
                ? WorkflowPrintTicketValidationStatus.Resolved
                : WorkflowPrintTicketValidationStatus.Conflicting;
        }
        catch (XmlException)
        {
            return WorkflowPrintTicketValidationStatus.Invalid;
        }
        catch (ArgumentException)
        {
            return WorkflowPrintTicketValidationStatus.Invalid;
        }
    }

    private void OnPrintDeviceCapabilitiesChanged(
        PrintSupportExtensionSession sender,
        PrintSupportPrintDeviceCapabilitiesChangedEventArgs args)
    {
        var deferral = args.GetDeferral();
        string printerName = GetPrinterName(sender);
        AppendDiagnostic(
            "Capabilities change received",
            printerName,
            "event=PrintDeviceCapabilitiesChanged");
        try
        {
            state.Run(
                () => UpdatePrintDeviceCapabilities(printerName, args),
                exception => AppendDiagnostic(
                    "Capabilities update failed",
                    printerName,
                    exception.ToString()));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static void UpdatePrintDeviceCapabilities(
        string printerName,
        PrintSupportPrintDeviceCapabilitiesChangedEventArgs args)
    {
        bool mxdcConfigured = false;
        bool pdrUpdated = false;
        bool ippTimeoutsConfigured = false;
        int resourceCount = 0;
        string mxdcQualityDetail = "mxdcQuality=<unavailable>";
        string pdrResourceNamesDetail = "pdrResourceNames=<none>";

        XmlDocument capabilities = args.GetCurrentPrintDeviceCapabilities();
        (XmlDocument updatedCapabilities, string pdcFeatureDetail, string pdcOptionDetail) =
            ApplyPrintSinkCapabilities(capabilities);
        args.UpdatePrintDeviceCapabilities(updatedCapabilities);

        string pdlPassthroughWithAttributesDetail =
            UniversalApiContract19PrintSupport.EnablePdlPassthroughWithJobAttributes(args);

        if (ApiInformation.IsPropertyPresent(PrintSupportCapabilitiesChangedEventArgsType, "MxdcImageQualityConfiguration"))
        {
            mxdcQualityDetail = ConfigureMxdcImageQuality(args.MxdcImageQualityConfiguration);
            mxdcConfigured = true;
        }

        if (ApiInformation.IsMethodPresent(PrintSupportCapabilitiesChangedEventArgsType, "GetCurrentPrintDeviceResources"))
        {
            XmlDocument resources = args.GetCurrentPrintDeviceResources();
            Dictionary<string, string> localizedResources = LoadLocalizedResources(args.ResourceLanguage);
            resourceCount = localizedResources.Count;
            pdrResourceNamesDetail = FormatLocalizedResourceNames(localizedResources);
            if (localizedResources.Count > 0)
            {
                XmlDocument updatedResources = ApplyPrintSinkResources(resources, localizedResources);
                args.UpdatePrintDeviceResources(updatedResources);
                pdrUpdated = true;
            }
        }

        if (ApiInformation.IsPropertyPresent(
            PrintSupportCapabilitiesChangedEventArgsType,
            "CommunicationConfiguration"))
        {
            ippTimeoutsConfigured = ConfigureIppCommunicationTimeouts(args.CommunicationConfiguration);
        }

        AppendDiagnostic(
            "Capabilities updated",
            printerName,
            string.Join(
                "; ",
                $"features={FormatBuiltInFeatureNames()}",
                pdcFeatureDetail,
                pdcOptionDetail,
                $"mxdc={(mxdcConfigured ? "configured" : "unavailable")}",
                mxdcQualityDetail,
                $"pdr={(pdrUpdated ? "updated" : "skipped")}",
                pdrResourceNamesDetail,
                $"ippTimeouts={(ippTimeoutsConfigured ? "configured" : "skipped")}",
                pdlPassthroughWithAttributesDetail,
                $"pdrResources={resourceCount}"));
    }

    private static string GetPrinterName(PrintSupportExtensionSession session)
    {
        try
        {
            return session.Printer.PrinterName;
        }
        catch (Exception ex) when (BackgroundTaskExceptionPolicy.IsRecoverable(ex))
        {
            return $"<unavailable:0x{ex.HResult:X8}>";
        }
    }

    private static (XmlDocument Document, string FeatureDetail, string OptionDetail) ApplyPrintSinkCapabilities(
        XmlDocument capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        XDocument sourceDocument = XDocument.Parse(capabilities.GetXml(), LoadOptions.PreserveWhitespace);
        XDocument updatedDocument = CapabilitiesEditor.Apply(sourceDocument, PrintSinkCapabilityFeatures.BuiltIn);
        XmlDocument result = new();
        result.LoadXml(updatedDocument.ToString(SaveOptions.DisableFormatting));
        return (result, FormatAppliedPdcFeatures(updatedDocument), FormatAppliedPdcOptions(updatedDocument));
    }

    private static string FormatAppliedPdcFeatures(XDocument document)
    {
        XElement root = document.Root ?? throw new ArgumentException("PDC document is empty.", nameof(document));
        string[] featureNames = [.. PrintSinkCapabilityFeatures.BuiltIn
            .Where(feature => root.Element(ToXName(feature.Name)) is not null)
            .Select(static feature => feature.Name.LocalName)];
        return "pdcFeatures=" + string.Join(",", featureNames);
    }

    private static string FormatAppliedPdcOptions(XDocument document)
    {
        XElement root = document.Root ?? throw new ArgumentException("PDC document is empty.", nameof(document));
        List<string> optionNames = [];
        foreach (CustomFeature feature in PrintSinkCapabilityFeatures.BuiltIn)
        {
            XElement? featureElement = root.Element(ToXName(feature.Name));
            if (featureElement is null)
            {
                continue;
            }

            optionNames.AddRange(feature.Options
                .Where(option => featureElement.Element(ToXName(option.Name)) is not null)
                .Select(static option => option.Name.LocalName));
        }

        return "pdcOptions=" + string.Join(",", optionNames);
    }

    private static XName ToXName(PrintSchemaQualifiedName name)
    {
        return XNamespace.Get(name.NamespaceUri.AbsoluteUri) + name.LocalName;
    }

    private static string ConfigureMxdcImageQuality(PrintSupportMxdcImageQualityConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        const XpsImageQuality text = XpsImageQuality.Png;
        const XpsImageQuality draft = XpsImageQuality.JpegHighCompression;
        const XpsImageQuality normal = XpsImageQuality.JpegMediumCompression;
        const XpsImageQuality high = XpsImageQuality.JpegLowCompression;
        const XpsImageQuality photographic = XpsImageQuality.Png;
        const XpsImageQuality automatic = XpsImageQuality.JpegMediumCompression;
        const XpsImageQuality fax = XpsImageQuality.JpegHighCompression;

        configuration.TextOutputQuality = text;
        configuration.DraftOutputQuality = draft;
        configuration.NormalOutputQuality = normal;
        configuration.HighOutputQuality = high;
        configuration.PhotographicOutputQuality = photographic;
        configuration.AutomaticOutputQuality = automatic;
        configuration.FaxOutputQuality = fax;

        return "mxdcQuality=" + string.Join(
            ",",
            $"Text={text}",
            $"Draft={draft}",
            $"Normal={normal}",
            $"High={high}",
            $"Photo={photographic}",
            $"Auto={automatic}",
            $"Fax={fax}");
    }

    private static bool ConfigureIppCommunicationTimeouts(PrintSupportIppCommunicationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.CanModifyTimeouts)
        {
            return false;
        }

        SetTimeouts(configuration.IppAttributeTimeouts, AttributeCommunicationTimeout);
        SetTimeouts(configuration.IppJobTimeouts, JobCommunicationTimeout);
        return true;
    }

    private static void SetTimeouts(PrintSupportIppCommunicationTimeouts timeouts, TimeSpan timeout)
    {
        timeouts.ConnectTimeout = timeout;
        timeouts.SendTimeout = timeout;
        timeouts.ReceiveTimeout = timeout;
    }

    private static XmlDocument ApplyPrintSinkResources(
        XmlDocument resources,
        IReadOnlyDictionary<string, string> localizedResources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(localizedResources);

        XDocument sourceDocument = string.IsNullOrWhiteSpace(resources.GetXml())
            ? new XDocument()
            : XDocument.Parse(resources.GetXml(), LoadOptions.PreserveWhitespace);
        XDocument updatedDocument = PrintDeviceResourcesEditor.Apply(sourceDocument, localizedResources);
        XmlDocument result = new();
        result.LoadXml(updatedDocument.ToString(SaveOptions.DisableFormatting));
        return result;
    }

    private static Dictionary<string, string> LoadLocalizedResources(string resourceLanguage)
    {
        ResourceContext resourceContext = ResourceContext.GetForViewIndependentUse();
        if (!string.IsNullOrWhiteSpace(resourceLanguage))
        {
            resourceContext.QualifierValues["language"] = resourceLanguage;
        }

        ResourceMap resourceMap = ResourceManager.Current.MainResourceMap.GetSubtree(PrintSinkFeatureResourceSubtree);
        Dictionary<string, string> resources = new(StringComparer.Ordinal);
        foreach (PrintSchemaQualifiedName name in CustomResourceNames)
        {
            if (!resourceMap.Keys.Contains(name.LocalName, StringComparer.Ordinal))
            {
                continue;
            }

            string localizedValue = resourceMap.GetValue(name.LocalName, resourceContext).ValueAsString;
            if (!string.IsNullOrWhiteSpace(localizedValue))
            {
                resources[ToPdrResourceName(name)] = localizedValue;
            }
        }

        return resources;
    }

    private static string FormatLocalizedResourceNames(Dictionary<string, string> localizedResources)
    {
        ArgumentNullException.ThrowIfNull(localizedResources);

        string[] resourceNames = [.. localizedResources.Keys
            .Select(static name =>
            {
                int separatorIndex = name.LastIndexOf('/');
                return separatorIndex < 0 ? name : name[(separatorIndex + 1)..];
            })
            .Order(StringComparer.Ordinal)];
        return "pdrResourceNames=" + string.Join(",", resourceNames);
    }

    private static IReadOnlyList<PrintSchemaQualifiedName> BuildCustomResourceNames()
    {
        Dictionary<string, PrintSchemaQualifiedName> names = new(StringComparer.Ordinal);
        foreach (CustomFeature feature in PrintSinkCapabilityFeatures.BuiltIn)
        {
            AddCustomResourceName(names, feature.Name);
            foreach (CustomFeatureOption option in feature.Options)
            {
                AddCustomResourceName(names, option.Name);
            }
        }

        return [.. names.Values];
    }

    private static void AddCustomResourceName(
        IDictionary<string, PrintSchemaQualifiedName> names,
        PrintSchemaQualifiedName name)
    {
        if (!string.Equals(name.NamespaceUri.AbsoluteUri, "https://schemas.printsink.dev/printing/keywords", StringComparison.Ordinal))
        {
            return;
        }

        names.TryAdd(ToPdrResourceName(name), name);
    }

    private static string ToPdrResourceName(PrintSchemaQualifiedName name)
    {
        string namespaceName = name.NamespaceUri.AbsoluteUri;
        if (namespaceName.StartsWith("https://", StringComparison.Ordinal))
        {
            namespaceName = namespaceName["https://".Length..];
        }
        else if (namespaceName.StartsWith("http://", StringComparison.Ordinal))
        {
            namespaceName = namespaceName["http://".Length..];
        }

        return string.Concat(namespaceName.TrimEnd('/'), "/", name.LocalName);
    }

    private void OnPrinterSelected(PrintSupportExtensionSession sender, PrintSupportPrinterSelectedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            state.Run(() =>
            {
                string printerName = sender.Printer.PrinterName;
                string cardText = $"PrintSink is managing {printerName}.";
                string json = $$"""
                    {"body":[{"type":"TextBlock","text":{{JsonSerializer.Serialize(cardText)}}}],"$schema":"http://adaptivecards.io/schemas/adaptive-card.json","type":"AdaptiveCard","version":"1.0"}
                    """;

                args.SetAdaptiveCard(Windows.UI.Shell.AdaptiveCardBuilder.CreateAdaptiveCardFromJson(json));
                string requestDetail = RequestAdditionalPrintDialogFields(args);
                AppendDiagnostic(
                    "Printer selected",
                    sender.Printer.PrinterName,
                    $"adaptiveCard=set; adaptiveCardVersion=1.0; adaptiveCardPrinter={SanitizeDiagnosticValue(printerName)}; {requestDetail}");
            });
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnCommunicationErrorDetected(
        PrintSupportExtensionSession sender,
        PrintSupportCommunicationErrorDetectedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            state.Run(() =>
            {
                bool timeoutsConfigured = false;
                if (args.ErrorKind == IppCommunicationErrorKind.Timeout)
                {
                    timeoutsConfigured = ConfigureIppCommunicationTimeouts(args.CommunicationConfiguration);
                }

                AppendDiagnostic(
                    "IPP communication error",
                    sender.Printer.PrinterName,
                    $"kind={args.ErrorKind}; timeouts={(timeoutsConfigured ? "configured" : "skipped")}");
            });
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static string RequestAdditionalPrintDialogFields(PrintSupportPrinterSelectedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        PrintSupportPrintTicketElement[] additionalFeatures =
        [
            CreatePrintTicketElement("PageMediaType"),
            CreatePrintTicketElement("PageOutputQuality"),
        ];
        PrintSupportPrintTicketElement[] additionalParameters =
        [
            CreatePrintTicketElement("JobCopiesAllDocuments"),
        ];

        uint requestedCount = (uint)(additionalFeatures.Length + additionalParameters.Length);
        if (requestedCount <= args.AllowedAdditionalFeaturesAndParametersCount)
        {
            args.SetAdditionalFeatures(additionalFeatures);
            args.SetAdditionalParameters(additionalParameters);
            return string.Join(
                "; ",
                "additionalFields=requested",
                $"allowed={args.AllowedAdditionalFeaturesAndParametersCount}",
                $"requested={requestedCount}",
                $"features={FormatPrintTicketElementNames(additionalFeatures)}",
                $"parameters={FormatPrintTicketElementNames(additionalParameters)}");
        }

        return string.Join(
            "; ",
            "additionalFields=skipped",
            $"allowed={args.AllowedAdditionalFeaturesAndParametersCount}",
            $"requested={requestedCount}");
    }

    private static PrintSupportPrintTicketElement CreatePrintTicketElement(string localName)
    {
        return new PrintSupportPrintTicketElement
        {
            LocalName = localName,
            NamespaceUri = PrintSchemaNamespaces.Keywords,
        };
    }

    private static string FormatBuiltInFeatureNames()
    {
        return string.Join(
            ",",
            PrintSinkCapabilityFeatures.BuiltIn.Select(static feature => feature.Name.LocalName));
    }

    private static string FormatPrintTicketElementNames(PrintSupportPrintTicketElement[] elements)
    {
        return string.Join(",", elements.Select(static element => element.LocalName));
    }

    private static string SanitizeDiagnosticValue(string value)
    {
        return value
            .ReplaceLineEndings(" ")
            .Replace(';', ',')
            .Trim();
    }

    private static void AppendDiagnostic(string message, string endpoint, string detail)
    {
        try
        {
            using LocalDiagnosticEventStore diagnosticEventStore = PackagedSettingsStoreFactory.CreateDiagnosticEventStore();
            diagnosticEventStore
                .AppendAsync(
                    new DiagnosticEventRecord(
                        DateTimeOffset.UtcNow,
                        DiagnosticEventSeverity.Information,
                        nameof(PrintSupportExtensionBackgroundTask),
                        message,
                        endpoint,
                        detail))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) when (BackgroundTaskExceptionPolicy.IsRecoverable(ex))
        {
            // Diagnostics must not make the PSA extension contract fail.
        }
    }
}
