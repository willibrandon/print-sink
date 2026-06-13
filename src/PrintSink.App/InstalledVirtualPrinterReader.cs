using Windows.ApplicationModel;
using Windows.Devices.Printers;
using PrintSink.Core.Endpoints;

namespace PrintSink.App;

/// <summary>
/// Reads PrintSink virtual printer state from the Windows print stack.
/// </summary>
internal static class InstalledVirtualPrinterReader
{
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
                        null);
            }

            return snapshots;
        }
        catch (Exception ex)
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
                    ex.Message));
        }
    }

    internal static string RefreshCapabilities(EndpointKind endpointKind)
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
        printDevice.RefreshPrintDeviceCapabilities();
        return $"Capabilities refreshed for {endpoint.QueueName}.";
    }

    internal static string AssertAttributeReadSupported(EndpointKind endpointKind)
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
        IDictionary<string, IppAttributeValue> attributes = printDevice.GetPrinterAttributes(
            ["document-format-default", "document-format-supported"]);

        string[] requiredAttributes = ["document-format-default", "document-format-supported"];
        string[] missingAttributes =
        [
            .. requiredAttributes.Where(attribute => !attributes.ContainsKey(attribute)),
        ];
        if (missingAttributes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Virtual printer attribute read for {endpoint.QueueName} missed attributes: {string.Join(",", missingAttributes)}");
        }

        string returnedAttributes = string.Join(
            "; ",
            attributes
                .OrderBy(attribute => attribute.Key, StringComparer.OrdinalIgnoreCase)
                .Select(attribute => $"{attribute.Key}={FormatKeywordValues(attribute.Value)}"));
        return $"Virtual printer attribute read succeeded for {endpoint.QueueName}: {returnedAttributes}";
    }

    private static InstalledVirtualPrinterSnapshot ReadInstalled(VirtualEndpoint endpoint)
    {
        try
        {
            IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
            return new InstalledVirtualPrinterSnapshot(
                endpoint.Kind,
                true,
                "Installed",
                printDevice.PrinterUri?.ToString(),
                printDevice.DeviceKind.ToString(),
                printDevice.CanModifyUserDefaultPrintTicket,
                printDevice.UserDefaultPrintTicket?.Name,
                null);
        }
        catch (Exception ex)
        {
            return new InstalledVirtualPrinterSnapshot(
                endpoint.Kind,
                true,
                "Installed, details unavailable",
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
}
