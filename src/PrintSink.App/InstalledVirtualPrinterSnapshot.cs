using PrintSink.Core.Endpoints;

namespace PrintSink.App;

/// <summary>
/// Captures the installed state reported by Windows for a PrintSink virtual printer.
/// </summary>
internal sealed class InstalledVirtualPrinterSnapshot
{
    internal InstalledVirtualPrinterSnapshot(
        EndpointKind endpointKind,
        bool isInstalled,
        string status,
        string? printerUri,
        string? deviceKind,
        bool? canModifyUserDefaultPrintTicket,
        string? userDefaultPrintTicketName,
        string? error)
    {
        EndpointKind = endpointKind;
        IsInstalled = isInstalled;
        Status = status;
        PrinterUri = printerUri;
        DeviceKind = deviceKind;
        CanModifyUserDefaultPrintTicket = canModifyUserDefaultPrintTicket;
        UserDefaultPrintTicketName = userDefaultPrintTicketName;
        Error = error;
    }

    internal EndpointKind EndpointKind { get; }

    internal bool IsInstalled { get; }

    internal string Status { get; }

    internal string? PrinterUri { get; }

    internal string? DeviceKind { get; }

    internal bool? CanModifyUserDefaultPrintTicket { get; }

    internal string? UserDefaultPrintTicketName { get; }

    internal string? Error { get; }
}
