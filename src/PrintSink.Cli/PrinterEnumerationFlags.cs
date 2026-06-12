namespace PrintSink.Cli;

[Flags]
internal enum PrinterEnumerationFlags : uint
{
    Local = 0x00000002,

    Connections = 0x00000004,
}
