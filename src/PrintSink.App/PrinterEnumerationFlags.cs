namespace PrintSink.App;

/// <summary>
/// Selects the printer scopes returned by the Windows spooler enumeration API.
/// </summary>
[Flags]
internal enum PrinterEnumerationFlags : uint
{
    /// <summary>
    /// Enumerates printers installed on the local machine.
    /// </summary>
    Local = 0x00000002,

    /// <summary>
    /// Enumerates printers connected for the current user.
    /// </summary>
    Connections = 0x00000004,
}
