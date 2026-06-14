using PrintSink.Core.Pdl;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;

namespace PrintSink.Tasks;

/// <summary>
/// Adapts Windows workflow PDL conversion to the core converter contract.
/// </summary>
internal sealed class WinRtPdlConverter : IPdlConverter
{
    private readonly PrintWorkflowVirtualPrinterDataAvailableEventArgs args;
    private readonly Func<WorkflowPrintTicket> getPrintTicket;

    internal WinRtPdlConverter(
        PrintWorkflowVirtualPrinterDataAvailableEventArgs args,
        Func<WorkflowPrintTicket> getPrintTicket)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(getPrintTicket);

        this.args = args;
        this.getPrintTicket = getPrintTicket;
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
        using (IInputStream converterInput = input.GetInputStreamAt(0))
        using (IOutputStream converterOutput = output.GetOutputStreamAt(0))
        {
            PrintWorkflowPdlConverter converter = args.GetPdlConverter(ToWinRtConversionType(conversionKind));
            await converter.ConvertPdlAsync(
                getPrintTicket(),
                converterInput,
                converterOutput).AsTask(cancellationToken).ConfigureAwait(false);
            await converterOutput.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }

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
