using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PrintSink.Cli;

/// <summary>
/// Reads installed printer queue names from the Windows print spooler.
/// </summary>
internal static partial class InstalledPrinterReader
{
    private const int ErrorInsufficientBuffer = 122;
    private const uint PrinterInfoLevel = 4;
    private const PrinterEnumerationFlags EnumerationScope =
        PrinterEnumerationFlags.Local | PrinterEnumerationFlags.Connections;

    internal static PrinterQueueSnapshot Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return PrinterQueueSnapshot.Unavailable("installed queue status is only available on Windows.");
        }

        try
        {
            return PrinterQueueSnapshot.Available(ReadPrinterNames());
        }
        catch (Win32Exception ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows printer enumeration failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows printer enumeration failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return PrinterQueueSnapshot.Unavailable($"Windows printer enumeration access was denied: {ex.Message}");
        }
    }

    private static string[] ReadPrinterNames()
    {
        bool measured = EnumPrinters(
            EnumerationScope,
            null,
            PrinterInfoLevel,
            0,
            0,
            out uint needed,
            out _);
        if (!measured && Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (needed == 0)
        {
            return [];
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)needed));
        try
        {
            if (!EnumPrinters(
                EnumerationScope,
                null,
                PrinterInfoLevel,
                buffer,
                needed,
                out _,
                out uint returned))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            int itemSize = Marshal.SizeOf<PrinterInfo4>();
            string[] names = new string[returned];
            for (int index = 0; index < names.Length; index++)
            {
                nint item = nint.Add(buffer, index * itemSize);
                PrinterInfo4 printerInfo = Marshal.PtrToStructure<PrinterInfo4>(item);
                names[index] = printerInfo.PrinterName ?? string.Empty;
            }

            return [.. names.Where(name => !string.IsNullOrWhiteSpace(name))];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumPrinters(
        PrinterEnumerationFlags flags,
        string? name,
        uint level,
        nint printerInfo,
        uint bufferSize,
        out uint needed,
        out uint returned);
}
