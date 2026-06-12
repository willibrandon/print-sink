using System.Text;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;
using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;

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

    /// <summary>
    /// Verifies job processing loads persisted watermark options for the endpoint.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_adds_persisted_watermark_options_to_sink_context()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        WatermarkOptions expected = new(
            true,
            new TextWatermark("Draft", "Segoe UI", 48, 0.35, -30, 0, 0),
            null);
        InMemorySettingsStore settingsStore = new();
        await settingsStore
            .SaveWatermarkOptionsAsync(endpoint.PrinterUri, expected)
            .ConfigureAwait(false);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.PdfContentType,
            endpoint,
            Encoding.UTF8.GetBytes("%PDF-1.7"),
            false);
        CapturingSink sink = new();
        VirtualPrinterJobProcessor processor = new(
            new PdlRouter(),
            new TestPdlConverter([]),
            new EndpointSinkResolver(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = sink,
            }),
            settingsStore);

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreSame(expected, sink.Context?.WatermarkOptions);
    }

    /// <summary>
    /// Verifies job UI options override endpoint defaults.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_uses_job_options_before_persisted_endpoint_watermark()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemorySettingsStore settingsStore = new();
        await settingsStore
            .SaveWatermarkOptionsAsync(
                endpoint.PrinterUri,
                new WatermarkOptions(true, new TextWatermark("Endpoint", "Segoe UI", 48, 0.35, -30, 0, 0), null))
            .ConfigureAwait(false);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.PdfContentType,
            endpoint,
            Encoding.UTF8.GetBytes("%PDF-1.7"),
            false);
        CapturingSink sink = new();
        VirtualPrinterJobProcessor processor = new(
            new PdlRouter(),
            new TestPdlConverter([]),
            new EndpointSinkResolver(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = sink,
            }),
            settingsStore,
            new JobProcessingOptions(WatermarkOptions.Disabled));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreSame(WatermarkOptions.Disabled, sink.Context?.WatermarkOptions);
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

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<Uri, WatermarkOptions> watermarkOptions = [];

        /// <inheritdoc />
        public Task<WatermarkOptions> GetWatermarkOptionsAsync(
            Uri printerUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(printerUri);

            return Task.FromResult(watermarkOptions.GetValueOrDefault(printerUri, WatermarkOptions.Disabled));
        }

        /// <inheritdoc />
        public Task SaveWatermarkOptionsAsync(
            Uri printerUri,
            WatermarkOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(printerUri);
            ArgumentNullException.ThrowIfNull(options);

            watermarkOptions[printerUri] = options;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SaveJobProcessingOptionsAsync(
            JobProcessingOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<JobProcessingOptions?> ConsumeJobProcessingOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<JobProcessingOptions?>(null);
        }
    }
}
