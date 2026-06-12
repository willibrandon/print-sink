using System.Text;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Tests virtual printer job processing.
/// </summary>
[TestClass]
public sealed class VirtualPrinterJobProcessorTests
{
    /// <summary>
    /// Verifies passthrough jobs are copied to the target stream and completed successfully.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_copies_passthrough_job_to_target_stream()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.PdfContentType,
            endpoint,
            Encoding.UTF8.GetBytes("%PDF-1.7"),
            true);
        TestPdlConverter converter = new(Encoding.UTF8.GetBytes("converted"));
        VirtualPrinterJobProcessor processor = CreateProcessor(new TargetStreamSink(), converter);

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, job.CompletedStatus);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("%PDF-1.7"), job.TargetBytes);
        Assert.AreEqual(0, converter.CallCount);
    }

    /// <summary>
    /// Verifies conversion jobs use the converter before writing to the sink.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_converts_job_before_sink_write()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            false);
        byte[] convertedBytes = Encoding.UTF8.GetBytes("%PDF-1.7 converted");
        TestPdlConverter converter = new(convertedBytes);
        CapturingSink sink = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(sink, converter);

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, job.CompletedStatus);
        Assert.AreEqual(1, converter.CallCount);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, converter.LastConversionKind);
        CollectionAssert.AreEqual(convertedBytes, sink.Bytes);
        Assert.AreEqual(PdlFormatInfo.PdfContentType, sink.Context?.ContentType);
    }

    /// <summary>
    /// Verifies rejected jobs complete as failed without opening streams.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_rejects_unknown_content_type()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            "application/octet-stream",
            endpoint,
            Encoding.UTF8.GetBytes("unknown"),
            false);
        CapturingSink sink = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(sink, new TestPdlConverter([]));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Failed, job.CompletedStatus);
        Assert.IsNull(result.Exception);
        Assert.AreEqual(PdlActionKind.Reject, result.Plan.ActionKind);
        CollectionAssert.AreEqual(Array.Empty<byte>(), sink.Bytes);
    }

    /// <summary>
    /// Verifies sink failures complete the job as failed and return the exception.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_marks_job_failed_when_sink_throws()
    {
        InvalidOperationException expected = new("sink failed");
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.PdfContentType,
            endpoint,
            Encoding.UTF8.GetBytes("%PDF-1.7"),
            false);
        VirtualPrinterJobProcessor processor = CreateProcessor(new CapturingSink(expected), new TestPdlConverter([]));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Failed, job.CompletedStatus);
        Assert.AreSame(expected, result.Exception);
    }

    private static VirtualPrinterJobProcessor CreateProcessor(ISink sink, IPdlConverter converter)
    {
        return new VirtualPrinterJobProcessor(
            new PdlRouter(),
            converter,
            new EndpointSinkResolver(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = sink,
            }));
    }
}
