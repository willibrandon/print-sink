using System.Runtime.InteropServices;

namespace PrintSink.Cli;

/// <summary>
/// Represents printer metadata returned by the Windows spooler.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrinterInfo2
{
    /// <summary>
    /// Gets the print server name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? ServerName;

    /// <summary>
    /// Gets the printer name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? PrinterName;

    /// <summary>
    /// Gets the shared printer name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? ShareName;

    /// <summary>
    /// Gets the printer port name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? PortName;

    /// <summary>
    /// Gets the printer driver name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? DriverName;

    /// <summary>
    /// Gets the printer comment.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? Comment;

    /// <summary>
    /// Gets the printer location.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? Location;

    /// <summary>
    /// Gets a pointer to the printer device mode.
    /// </summary>
    internal nint DevMode;

    /// <summary>
    /// Gets the separator file path.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? SeparatorFile;

    /// <summary>
    /// Gets the print processor name.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? PrintProcessor;

    /// <summary>
    /// Gets the default data type.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? DataType;

    /// <summary>
    /// Gets print processor parameters.
    /// </summary>
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? Parameters;

    /// <summary>
    /// Gets a pointer to the printer security descriptor.
    /// </summary>
    internal nint SecurityDescriptor;

    /// <summary>
    /// Gets printer attributes.
    /// </summary>
    internal uint Attributes;

    /// <summary>
    /// Gets the printer priority.
    /// </summary>
    internal uint Priority;

    /// <summary>
    /// Gets the default job priority.
    /// </summary>
    internal uint DefaultPriority;

    /// <summary>
    /// Gets the daily start time.
    /// </summary>
    internal uint StartTime;

    /// <summary>
    /// Gets the daily end time.
    /// </summary>
    internal uint UntilTime;

    /// <summary>
    /// Gets printer status flags.
    /// </summary>
    internal uint Status;

    /// <summary>
    /// Gets the number of queued jobs.
    /// </summary>
    internal uint Jobs;

    /// <summary>
    /// Gets the average pages per minute.
    /// </summary>
    internal uint AveragePagesPerMinute;
}
