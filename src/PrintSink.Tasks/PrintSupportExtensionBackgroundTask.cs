using System.Xml.Linq;
using PrintSink.Core.Capabilities;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Metadata;
using Windows.Graphics.Printing.PrintSupport;

namespace PrintSink.Tasks;

/// <summary>
/// Handles the shared Print Support extension background contract.
/// </summary>
public sealed class PrintSupportExtensionBackgroundTask : IBackgroundTask
{
    private const string PrintSupportExtensionSessionType =
        "Windows.Graphics.Printing.PrintSupport.PrintSupportExtensionSession";

    private static readonly PrintDeviceCapabilitiesEditor CapabilitiesEditor = new();

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
        session.PrintTicketValidationRequested += OnPrintTicketValidationRequested;
        session.PrintDeviceCapabilitiesChanged += OnPrintDeviceCapabilitiesChanged;

        if (ApiInformation.IsEventPresent(PrintSupportExtensionSessionType, "PrinterSelected"))
        {
            session.PrinterSelected += OnPrinterSelected;
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
            state.Run(() => args.SetPrintTicketValidationStatus(WorkflowPrintTicketValidationStatus.Resolved));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnPrintDeviceCapabilitiesChanged(
        PrintSupportExtensionSession sender,
        PrintSupportPrintDeviceCapabilitiesChangedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            state.Run(() =>
            {
                XmlDocument capabilities = args.GetCurrentPrintDeviceCapabilities();
                XmlDocument updatedCapabilities = ApplyPrintSinkCapabilities(capabilities);
                args.UpdatePrintDeviceCapabilities(updatedCapabilities);
            });
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static XmlDocument ApplyPrintSinkCapabilities(XmlDocument capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        XDocument sourceDocument = XDocument.Parse(capabilities.GetXml(), LoadOptions.PreserveWhitespace);
        XDocument updatedDocument = CapabilitiesEditor.Apply(sourceDocument, PrintSinkCapabilityFeatures.BuiltIn);
        XmlDocument result = new();
        result.LoadXml(updatedDocument.ToString(SaveOptions.DisableFormatting));
        return result;
    }

    private void OnPrinterSelected(PrintSupportExtensionSession sender, PrintSupportPrinterSelectedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            state.Run(() =>
            {
                string printerName = sender.Printer.PrinterName.Replace("\\", "\\\\", StringComparison.Ordinal);
                string json = $$"""
                    {"body":[{"type":"TextBlock","text":"PrintSink is managing {{printerName}}."}],"$schema":"http://adaptivecards.io/schemas/adaptive-card.json","type":"AdaptiveCard","version":"1.0"}
                    """;

                args.SetAdaptiveCard(Windows.UI.Shell.AdaptiveCardBuilder.CreateAdaptiveCardFromJson(json));
            });
        }
        finally
        {
            deferral.Complete();
        }
    }
}
