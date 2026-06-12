using PrintSink.Core.Pdl;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;

namespace PrintSink.Tasks;

internal sealed class WinRtPdlConverter : IPdlConverter
{
    private readonly PrintWorkflowVirtualPrinterDataAvailableEventArgs args;
    private readonly WorkflowPrintTicket printTicket;

    internal WinRtPdlConverter(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args,
        WorkflowPrintTicket printTicket)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(printTicket);

        this.args = args;
        this.printTicket = printTicket;
    }

    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(
        Stream source,
        PdlConversionKind conversionKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using InMemoryRandomAccessStream input = new();
        using (IOutputStream inputOutput = input.GetOutputStreamAt(0))
        {
            await WinRtStreamBridge.WriteFromStreamAsync(source, inputOutput, cancellationToken).ConfigureAwait(false);
        }

        input.Seek(0);

        using InMemoryRandomAccessStream output = new();
        using IInputStream converterInput = input.GetInputStreamAt(0);
        using IOutputStream converterOutput = output.GetOutputStreamAt(0);
        PrintWorkflowPdlConverter converter = args.GetPdlConverter(ToWinRtConversionType(conversionKind));
        await converter.ConvertPdlAsync(
            printTicket,
            converterInput,
            converterOutput).AsTask(cancellationToken).ConfigureAwait(false);

        output.Seek(0);
        using IInputStream convertedInput = output.GetInputStreamAt(0);
        return await WinRtStreamBridge.ReadToMemoryAsync(convertedInput, cancellationToken).ConfigureAwait(false);
    }

    private static PrintWorkflowPdlConversionType ToWinRtConversionType(PdlConversionKind conversionKind)
    {
        return conversionKind switch
        {
            PdlConversionKind.XpsToPdf => PrintWorkflowPdlConversionType.XpsToPdf,
            PdlConversionKind.XpsToPwgRaster => PrintWorkflowPdlConversionType.XpsToPwgr,
            PdlConversionKind.XpsToPclm => PrintWorkflowPdlConversionType.XpsToPclm,
            _ => throw new ArgumentOutOfRangeException(nameof(conversionKind), conversionKind, "Unknown PDL conversion kind."),
        };
    }
}
