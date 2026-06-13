using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Storage.Streams;

namespace PrintSink.App;

/// <summary>
/// Sends source PDL through the Windows PDL passthrough provider.
/// </summary>
internal static class PdlPassthroughPrintCommand
{
    private const int BufferSize = 81920;

    internal static async Task<int> PrintPdfAsync(
        EndpointKind endpointKind,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        if (!endpoint.SupportsPassthrough(PdlFormat.Pdf))
        {
            throw new ArgumentException(
                $"Endpoint '{endpoint.QueueName}' does not support PDF passthrough.",
                nameof(endpointKind));
        }

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("PDF passthrough source was not found.", fullSourcePath);
        }

        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
        if (!printDevice.IsPdlPassthroughSupported(PdlFormatInfo.PdfContentType))
        {
            throw new InvalidOperationException(
                $"Printer '{endpoint.QueueName}' does not report PDF passthrough support.");
        }

        PdlPassthroughProvider provider = printDevice.GetPdlPassthroughProvider();
        using InMemoryRandomAccessStream printTicketStream = await CreatePrintTicketStreamAsync(
                printDevice.UserDefaultPrintTicket,
                cancellationToken)
            .ConfigureAwait(false);
        using IInputStream printTicketInput = printTicketStream.GetInputStreamAt(0);
        PageConfigurationSettings pageConfiguration = new()
        {
            OrientationSource = PageConfigurationSource.PdlContent,
            SizeSource = PageConfigurationSource.PdlContent,
        };

        string jobName = Path.GetFileNameWithoutExtension(fullSourcePath);
        using PdlPassthroughTarget target = provider.StartPrintJobWithPrintTicket(
            jobName,
            PdlFormatInfo.PdfContentType,
            printTicketInput,
            pageConfiguration);
        int printJobId = target.PrintJobId;
        using IOutputStream output = target.GetOutputStream();
        await WriteFileToOutputAsync(fullSourcePath, output, cancellationToken).ConfigureAwait(false);

        target.Submit();
        return printJobId;
    }

    private static async Task<InMemoryRandomAccessStream> CreatePrintTicketStreamAsync(
        WorkflowPrintTicket printTicket,
        CancellationToken cancellationToken)
    {
        string printTicketXml = printTicket.XmlNode.GetXml();
        InMemoryRandomAccessStream stream = new();
        using IOutputStream output = stream.GetOutputStreamAt(0);
        using DataWriter writer = new(output)
        {
            UnicodeEncoding = UnicodeEncoding.Utf8,
        };

        writer.WriteString(printTicketXml);
        await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        writer.DetachStream();
        stream.Seek(0);
        return stream;
    }

    private static async Task WriteFileToOutputAsync(
        string sourcePath,
        IOutputStream output,
        CancellationToken cancellationToken)
    {
        await using FileStream source = File.OpenRead(sourcePath);
        using DataWriter writer = new(output);
        byte[] buffer = new byte[BufferSize];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (read == buffer.Length)
            {
                writer.WriteBytes(buffer);
            }
            else
            {
                byte[] slice = new byte[read];
                System.Buffer.BlockCopy(buffer, 0, slice, 0, read);
                writer.WriteBytes(slice);
            }

            await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        writer.DetachStream();
    }
}
