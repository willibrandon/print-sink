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
}
