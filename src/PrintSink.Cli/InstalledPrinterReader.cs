using Microsoft.Win32;
using System.Security;

namespace PrintSink.Cli;

/// <summary>
/// Reads installed printer queue names from the local Windows print registry.
/// </summary>
internal static class InstalledPrinterReader
{
    private const string PrintersRegistryPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";

    internal static PrinterQueueSnapshot Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return PrinterQueueSnapshot.Unavailable("installed queue status is only available on Windows.");
        }

        try
        {
            using RegistryKey? printersKey = Registry.LocalMachine.OpenSubKey(PrintersRegistryPath);
            if (printersKey is null)
            {
                return PrinterQueueSnapshot.Unavailable("Windows print registry key was not found.");
            }

            return PrinterQueueSnapshot.Available(printersKey.GetSubKeyNames());
        }
        catch (IOException ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows print registry could not be read: {ex.Message}");
        }
        catch (SecurityException ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows print registry access was denied: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows print registry access was denied: {ex.Message}");
        }
    }
}
