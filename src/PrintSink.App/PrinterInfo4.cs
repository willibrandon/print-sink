using System.Runtime.InteropServices;

namespace PrintSink.App;

/// <summary>
/// Represents the compact printer metadata returned by the Windows spooler.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrinterInfo4
{
    /// <summary>
    /// Gets the printer name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? PrinterName;

    /// <summary>
    /// Gets the print server name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? ServerName;

    /// <summary>
    /// Gets printer attributes.
    /// </summary>
    internal uint Attributes;
}
