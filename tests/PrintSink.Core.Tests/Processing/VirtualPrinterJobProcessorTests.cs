using System.Text;
using PrintSink.Endpoints;
using PrintSink.Pdl;
using PrintSink.Processing;
using PrintSink.Settings;
using PrintSink.Watermark;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Tests for <see cref="VirtualPrinterJobProcessor"/>.
/// </summary>
[TestClass]
internal sealed class VirtualPrinterJobProcessorTests
{
    private const string PrintTicketXml = """
        <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" />
        """;

    /// <summary>
    /// Gets or sets the MSTest context for the current test.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies PDF passthrough writes original bytes to the sink.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ProcessAsyncPdfPassthroughWritesOriginalBytes()
    {
        RecordingSink sink = new();
        RecordingPdlConverter converter = new();
        RecordingXpsWatermarkProcessor watermarker = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(converter, watermarker, WatermarkOptions.Disabled);
        byte[] source = Encoding.UTF8.GetBytes("%PDF-1.7");
        TestVirtualPrinterJob job = new(PdlFormatInfo.PdfContentType, EndpointCatalog.Pdf, source, sink, PrintTicketXml);

        VirtualPrinterProcessingResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterProcessingStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(source, sink.Bytes);
        Assert.AreEqual(PdlFormatInfo.PdfContentType, sink.Context?.ContentType);
        Assert.IsFalse(watermarker.WasInvoked);
        Assert.IsFalse(job.WasPrintTicketRead);
    }

    /// <summary>
    /// Verifies OXPS-to-PDF conversion uses the converter and print ticket.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ProcessAsyncOxpsToPdfConvertsWithPrintTicket()
    {
        RecordingSink sink = new();
        RecordingPdlConverter converter = new();
        RecordingXpsWatermarkProcessor watermarker = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(converter, watermarker, WatermarkOptions.Disabled);
        TestVirtualPrinterJob job = new(PdlFormatInfo.OxpsContentType, EndpointCatalog.Pdf, Encoding.UTF8.GetBytes("oxps"), sink, PrintTicketXml);

        VirtualPrinterProcessingResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterProcessingStatus.Succeeded, result.Status);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, converter.Conversion);
        Assert.AreEqual(PrintTicketXml, converter.PrintTicketXml);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("converted:XpsToPdf"), sink.Bytes);
        Assert.IsTrue(job.WasPrintTicketRead);
        Assert.IsFalse(watermarker.WasInvoked);
    }

    /// <summary>
    /// Verifies watermarking happens before conversion when watermark options are enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ProcessAsyncWatermarkedOxpsWatermarksBeforeConversion()
    {
        RecordingSink sink = new();
        RecordingPdlConverter converter = new();
        RecordingXpsWatermarkProcessor watermarker = new();
        WatermarkOptions watermark = new(
            isTextEnabled: true,
            text: new TextWatermark("Draft", 36, 0.4, -30, 0, 0),
            isImageEnabled: false,
            image: null);
        VirtualPrinterJobProcessor processor = CreateProcessor(converter, watermarker, watermark);
        TestVirtualPrinterJob job = new(PdlFormatInfo.OxpsContentType, EndpointCatalog.Pdf, Encoding.UTF8.GetBytes("oxps"), sink, PrintTicketXml);

        VirtualPrinterProcessingResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterProcessingStatus.Succeeded, result.Status);
        Assert.IsTrue(watermarker.WasInvoked);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("watermarked:oxps"), converter.SourceBytes);
    }

    /// <summary>
    /// Verifies unsupported content is rejected without opening source or sink.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ProcessAsyncUnsupportedContentReturnsRejected()
    {
        RecordingSink sink = new();
        RecordingPdlConverter converter = new();
        RecordingXpsWatermarkProcessor watermarker = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(converter, watermarker, WatermarkOptions.Disabled);
        TestVirtualPrinterJob job = new("application/example", EndpointCatalog.Pdf, Encoding.UTF8.GetBytes("bad"), sink, PrintTicketXml);

        VirtualPrinterProcessingResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterProcessingStatus.Rejected, result.Status);
        Assert.IsFalse(job.WasSourceOpened);
        Assert.IsFalse(job.WasSinkOpened);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>
    /// Verifies runner cancellation is returned as a canceled result.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task ProcessAsyncCanceledTokenReturnsCanceled()
    {
        RecordingSink sink = new();
        RecordingPdlConverter converter = new();
        RecordingXpsWatermarkProcessor watermarker = new();
        VirtualPrinterJobProcessor processor = CreateProcessor(converter, watermarker, WatermarkOptions.Disabled);
        TestVirtualPrinterJob job = new(PdlFormatInfo.PdfContentType, EndpointCatalog.Pdf, Encoding.UTF8.GetBytes("pdf"), sink, PrintTicketXml);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await cancellation.CancelAsync().ConfigureAwait(false);

        VirtualPrinterProcessingResult result = await processor.ProcessAsync(job, cancellation.Token).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterProcessingStatus.Canceled, result.Status);
    }

    private static VirtualPrinterJobProcessor CreateProcessor(
        RecordingPdlConverter converter,
        RecordingXpsWatermarkProcessor watermarker,
        WatermarkOptions watermarkOptions)
    {
        InMemorySettingsStore store = new();
        WatermarkSettingsService settings = new(store);
        if (watermarkOptions.IsEnabled)
        {
            settings.SaveAsync(watermarkOptions).AsTask().GetAwaiter().GetResult();
        }

        return new VirtualPrinterJobProcessor(new PdlRouter(), converter, watermarker, settings);
    }
}
