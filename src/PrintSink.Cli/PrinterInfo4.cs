using System.Runtime.InteropServices;

namespace PrintSink.Cli;

[StructLayout(LayoutKind.Sequential)]
internal struct PrinterInfo4
{
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? PrinterName;

    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? ServerName;

    internal uint Attributes;
}
