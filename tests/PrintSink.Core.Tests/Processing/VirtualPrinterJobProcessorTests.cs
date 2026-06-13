using System.Text;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Diagnostics;
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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, job.CompletedStatus);
        Assert.AreEqual(1, converter.CallCount);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, converter.LastConversionKind);
        CollectionAssert.AreEqual(convertedBytes, sink.Bytes);
        Assert.AreEqual(PdlFormatInfo.PdfContentType, sink.Context?.ContentType);
    }

    /// <summary>
    /// Verifies empty converted output fails the job instead of producing an empty target file.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_fails_file_job_when_converted_output_is_empty()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            true);
        VirtualPrinterJobProcessor processor = CreateProcessor(new TargetStreamSink(), new TestPdlConverter([]));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Failed, job.CompletedStatus);
        Assert.IsInstanceOfType<InvalidOperationException>(result.Exception);
        CollectionAssert.AreEqual(Array.Empty<byte>(), job.TargetBytes);
    }

    /// <summary>
    /// Verifies transformed XPS bytes are passed to the converter before sink writes.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_transforms_job_before_conversion()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        byte[] sourceBytes = Encoding.UTF8.GetBytes("xps");
        byte[] transformedBytes = Encoding.UTF8.GetBytes("watermarked xps");
        byte[] convertedBytes = Encoding.UTF8.GetBytes("%PDF-1.7 converted");
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            sourceBytes,
            false);
        TestPdlTransformer transformer = new(transformedBytes);
        TestPdlConverter converter = new(convertedBytes);
        CapturingSink sink = new();
        WatermarkOptions watermarkOptions = new(
            true,
            new TextWatermark("Draft", "Segoe UI", 48, 0.35, -30, 0, 0),
            null);
        VirtualPrinterJobProcessor processor = CreateProcessor(
            sink,
            converter,
            transformer,
            null,
            new JobProcessingOptions(watermarkOptions));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreEqual(1, transformer.CallCount);
        CollectionAssert.AreEqual(sourceBytes, transformer.LastSourceBytes);
        Assert.AreSame(watermarkOptions, transformer.LastWatermarkOptions);
        CollectionAssert.AreEqual(transformedBytes, converter.LastSourceBytes);
        CollectionAssert.AreEqual(convertedBytes, sink.Bytes);
    }

    /// <summary>
    /// Verifies transformed passthrough bytes are written directly to the target stream.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_writes_transformed_passthrough_job_to_target_stream()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Xps);
        byte[] sourceBytes = Encoding.UTF8.GetBytes("xps");
        byte[] transformedBytes = Encoding.UTF8.GetBytes("watermarked xps");
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            sourceBytes,
            true);
        TestPdlTransformer transformer = new(transformedBytes);
        TestPdlConverter converter = new(Encoding.UTF8.GetBytes("converted"));
        VirtualPrinterJobProcessor processor = CreateProcessor(
            new TargetStreamSink(),
            converter,
            transformer,
            null,
            new JobProcessingOptions(WatermarkOptions.Disabled));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreEqual(1, transformer.CallCount);
        Assert.AreEqual(0, converter.CallCount);
        CollectionAssert.AreEqual(sourceBytes, transformer.LastSourceBytes);
        Assert.AreSame(WatermarkOptions.Disabled, transformer.LastWatermarkOptions);
        CollectionAssert.AreEqual(transformedBytes, job.TargetBytes);
    }

    /// <summary>
    /// Verifies transform failures complete the job as failed.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_marks_job_failed_when_transform_throws()
    {
        InvalidOperationException expected = new("transform failed");
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            false);
        TestPdlTransformer transformer = new([], expected);
        VirtualPrinterJobProcessor processor = CreateProcessor(
            new CapturingSink(),
            new TestPdlConverter([]),
            transformer,
            null,
            new JobProcessingOptions(WatermarkOptions.Disabled));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Failed, job.CompletedStatus);
        Assert.AreSame(expected, result.Exception);
    }

    /// <summary>
    /// Verifies failure diagnostics include exception identity when the exception message is empty.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_records_exception_identity_when_failure_message_is_empty()
    {
        string diagnosticDirectory = Path.Combine(TestContext.TestRunResultsDirectory!, Guid.NewGuid().ToString("N"));
        LocalDiagnosticEventStore diagnosticEventStore = new(diagnosticDirectory);
        InvalidOperationException expected = new(string.Empty);
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            false);
        TestPdlTransformer transformer = new([], expected);
        VirtualPrinterJobProcessor processor = new(
            new PdlRouter(),
            new TestPdlConverter([]),
            new EndpointSinkResolver(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = new CapturingSink(),
            }),
            null,
            new JobProcessingOptions(WatermarkOptions.Disabled),
            transformer,
            diagnosticEventStore);

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        IReadOnlyList<DiagnosticEventRecord> events = await diagnosticEventStore
            .ReadRecentAsync(8, TestContext.CancellationToken)
            .ConfigureAwait(false);
        DiagnosticEventRecord failure = events.Single(entry => entry.Message == "Job failed");
        string detail = failure.Detail ?? string.Empty;
        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.AreSame(expected, result.Exception);
        Assert.Contains(nameof(InvalidOperationException), detail);
        Assert.Contains("0x", detail);
        Assert.Contains("route=application/oxps -> Pdf; Convert; Convert XPS to PDF.", detail);
    }

    /// <summary>
    /// Verifies transform cancellation completes the job as canceled.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_marks_job_canceled_when_transform_cancels()
    {
        OperationCanceledException expected = new("transform canceled");
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            false);
        TestPdlTransformer transformer = new([], expected);
        VirtualPrinterJobProcessor processor = CreateProcessor(
            new CapturingSink(),
            new TestPdlConverter([]),
            transformer,
            null,
            new JobProcessingOptions(WatermarkOptions.Disabled));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Canceled, result.Status);
        Assert.AreEqual(VirtualPrinterJobStatus.Canceled, job.CompletedStatus);
        Assert.AreSame(expected, result.Exception);
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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

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
            .SaveWatermarkOptionsAsync(endpoint.PrinterUri, expected, TestContext.CancellationToken)
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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

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
                new WatermarkOptions(true, new TextWatermark("Endpoint", "Segoe UI", 48, 0.35, -30, 0, 0), null), TestContext.CancellationToken)
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

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.AreSame(WatermarkOptions.Disabled, sink.Context?.WatermarkOptions);
    }

    /// <summary>
    /// Verifies job password metadata is recorded without exposing the password.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_records_job_password_metadata_without_secret()
    {
        string diagnosticDirectory = Path.Combine(TestContext.TestRunResultsDirectory!, Guid.NewGuid().ToString("N"));
        LocalDiagnosticEventStore diagnosticEventStore = new(diagnosticDirectory);
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
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
            null,
            new JobProcessingOptions(
                WatermarkOptions.Disabled,
                JobPasswordOptions.FromPassword("secret", "sha2-256")),
            PassThroughPdlTransformer.Instance,
            diagnosticEventStore);

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        IReadOnlyList<DiagnosticEventRecord> events = await diagnosticEventStore
            .ReadRecentAsync(8, TestContext.CancellationToken)
            .ConfigureAwait(false);
        DiagnosticEventRecord completion = events.Single(entry => entry.Message == "Job completed");
        string detail = completion.Detail ?? string.Empty;
        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.Contains("job-password=present-not-applicable", detail);
        Assert.DoesNotContain("secret", detail);
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

    private static VirtualPrinterJobProcessor CreateProcessor(
        ISink sink,
        IPdlConverter converter,
        IPdlTransformer transformer,
        ISettingsStore? settingsStore,
        JobProcessingOptions? jobProcessingOptions)
    {
        return new VirtualPrinterJobProcessor(
            new PdlRouter(),
            converter,
            new EndpointSinkResolver(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = sink,
                [EndpointKind.Xps] = sink,
            }),
            settingsStore,
            jobProcessingOptions,
            transformer);
    }

    /// <summary>
    /// Gets or sets the current MSTest context.
    /// </summary>
    /// <value>The current MSTest context.</value>
    public TestContext TestContext { get; set; } = null!;
}
