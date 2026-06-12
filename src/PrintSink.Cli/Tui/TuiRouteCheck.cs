using PrintSink.Core.Abstractions;
using PrintSink.Core.Pdl;

namespace PrintSink.Cli.Tui;

internal sealed class TuiRouteCheck
{
    internal TuiRouteCheck(
        string queueName,
        string contentType,
        PdlActionKind actionKind,
        PdlConversionKind? conversionKind,
        VirtualPrinterJobStatus status,
        long outputBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        QueueName = queueName;
        ContentType = contentType;
        ActionKind = actionKind;
        ConversionKind = conversionKind;
        Status = status;
        OutputBytes = outputBytes;
    }

    internal string QueueName { get; }

    internal string ContentType { get; }

    internal PdlActionKind ActionKind { get; }

    internal PdlConversionKind? ConversionKind { get; }

    internal VirtualPrinterJobStatus Status { get; }

    internal long OutputBytes { get; }
}
