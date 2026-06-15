using Windows.ApplicationModel;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.PrintTicket;
using PrintSink.Core.Endpoints;

namespace PrintSink.App;

/// <summary>
/// Reads PrintSink virtual printer state from the Windows print stack.
/// </summary>
internal static class InstalledVirtualPrinterReader
{
    private const int RefreshCapabilitiesMaximumAttempts = 30;
    private static readonly TimeSpan RefreshCapabilitiesRetryDelay = TimeSpan.FromSeconds(3);

    internal static IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> ReadAll()
    {
        try
        {
            HashSet<string> installedPrinterNames = VirtualPrinterManager
                .FindAllVirtualPrinters(Package.Current.Id.FamilyName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Dictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = [];
            foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
            {
                snapshots[endpoint.Kind] = installedPrinterNames.Contains(endpoint.QueueName)
                    ? ReadInstalled(endpoint)
                    : new InstalledVirtualPrinterSnapshot(
                        endpoint.Kind,
                        false,
                        "Missing",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);
            }

            return snapshots;
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            return EndpointCatalog.All.ToDictionary(
                endpoint => endpoint.Kind,
                endpoint => new InstalledVirtualPrinterSnapshot(
                    endpoint.Kind,
                    false,
                    "Unavailable",
                    null,
                    null,
                    null,
                    null,
                    null,
                    ex.Message));
        }
    }

    internal static string RefreshCapabilities(EndpointKind endpointKind)
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
                printDevice.RefreshPrintDeviceCapabilities();
                return attempt == 1
                    ? $"Capabilities refreshed for {endpoint.QueueName}."
                    : $"Capabilities refreshed for {endpoint.QueueName} after {attempt} attempts.";
            }
            catch (TimeoutException) when (attempt < RefreshCapabilitiesMaximumAttempts)
            {
                Thread.Sleep(RefreshCapabilitiesRetryDelay);
            }
        }
    }

    internal static string AssertAttributeReadMatchesPlatformBehavior(EndpointKind endpointKind)
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
        IDictionary<string, IppAttributeValue> attributes = printDevice.GetPrinterAttributes(
            ["document-format-default", "document-format-supported"]);

        string[] requiredAttributes = ["document-format-default", "document-format-supported"];
        string[] supportedAttributes =
        [
            .. requiredAttributes.Where(
                attribute => attributes.TryGetValue(attribute, out IppAttributeValue? value)
                    && value.GetKeywordArray().Count > 0),
        ];
        if (supportedAttributes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Virtual printer attribute read for {endpoint.QueueName} returned usable document-format attributes: {string.Join(",", supportedAttributes)}");
        }

        string returnedAttributes = string.Join(
            "; ",
            requiredAttributes.Select(attribute => $"{attribute}={FormatUnsupportedAttribute(attribute, attributes)}"));
        return $"Virtual printer attribute read matched platform behavior for {endpoint.QueueName}: {returnedAttributes}";
    }

    private static InstalledVirtualPrinterSnapshot ReadInstalled(VirtualEndpoint endpoint)
    {
        try
        {
            IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
            WorkflowPrintTicket? defaultPrintTicket = printDevice.UserDefaultPrintTicket;
            return new InstalledVirtualPrinterSnapshot(
                endpoint.Kind,
                true,
                "Installed",
                printDevice.PrinterUri?.ToString(),
                printDevice.DeviceKind.ToString(),
                printDevice.CanModifyUserDefaultPrintTicket,
                defaultPrintTicket?.Name,
                defaultPrintTicket is null ? null : UserDefaultPrintTicketEditor.ReadCopies(defaultPrintTicket),
                null);
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            return new InstalledVirtualPrinterSnapshot(
                endpoint.Kind,
                true,
                "Installed, details unavailable",
                null,
                null,
                null,
                null,
                null,
                ex.Message);
        }
    }

    private static string FormatKeywordValues(IppAttributeValue attribute)
    {
        IList<string> values = attribute.GetKeywordArray();
        return values.Count == 0
            ? "<empty>"
            : string.Join(",", values);
    }

    private static string FormatUnsupportedAttribute(
        string attributeName,
        IDictionary<string, IppAttributeValue> attributes)
    {
        return attributes.TryGetValue(attributeName, out IppAttributeValue? attribute)
            ? FormatKeywordValues(attribute).Replace("<empty>", "<unsupported>", StringComparison.Ordinal)
            : "<unsupported>";
    }
}
