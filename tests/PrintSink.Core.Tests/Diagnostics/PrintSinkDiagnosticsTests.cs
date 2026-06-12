using System.Text;
using System.Diagnostics.Tracing;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;
using PrintSink.Core.Tests.Processing;

namespace PrintSink.Core.Tests.Diagnostics;

/// <summary>
/// Tests the PrintSink diagnostics event source.
/// </summary>
[TestClass]
public sealed class PrintSinkDiagnosticsTests
{
    /// <summary>
    /// Verifies the provider name used for ETW/EventSource listeners.
    /// </summary>
    [TestMethod]
    public void Log_uses_expected_provider_name()
    {
        Assert.AreEqual("PrintSink-Diagnostics", PrintSinkDiagnostics.Log.Name);
    }

    /// <summary>
    /// Verifies successful conversion jobs emit routing, conversion, and completion events.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_emits_success_events()
    {
        using CollectingEventListener listener = new();
        listener.EnableEvents(PrintSinkDiagnostics.Log, EventLevel.LogAlways);
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.OxpsContentType,
            endpoint,
            Encoding.UTF8.GetBytes("xps"),
            false);
        VirtualPrinterJobProcessor processor = CreateProcessor(new CapturingSink(), new TestPdlConverter(Encoding.UTF8.GetBytes("pdf")));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Succeeded, result.Status);
        Assert.Contains("JobRouteResolved", listener.EventNames);
        Assert.Contains("PdlConversionStarted", listener.EventNames);
        Assert.Contains("PdlConversionCompleted", listener.EventNames);
        Assert.Contains("JobCompleted", listener.EventNames);
    }

    /// <summary>
    /// Verifies rejected jobs emit a rejection event.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_emits_rejection_event()
    {
        using CollectingEventListener listener = new();
        listener.EnableEvents(PrintSinkDiagnostics.Log, EventLevel.LogAlways);
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            "application/octet-stream",
            endpoint,
            Encoding.UTF8.GetBytes("unknown"),
            false);
        VirtualPrinterJobProcessor processor = CreateProcessor(new CapturingSink(), new TestPdlConverter([]));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.Contains("JobRouteResolved", listener.EventNames);
        Assert.Contains("JobRejected", listener.EventNames);
    }

    /// <summary>
    /// Verifies failed sink writes emit a failure event.
    /// </summary>
    [TestMethod]
    public async Task ProcessAsync_emits_failure_event()
    {
        using CollectingEventListener listener = new();
        listener.EnableEvents(PrintSinkDiagnostics.Log, EventLevel.LogAlways);
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);
        InMemoryVirtualPrinterJob job = new(
            PdlFormatInfo.PdfContentType,
            endpoint,
            Encoding.UTF8.GetBytes("%PDF-1.7"),
            false);
        VirtualPrinterJobProcessor processor = CreateProcessor(
            new CapturingSink(new InvalidOperationException("sink failed")),
            new TestPdlConverter([]));

        VirtualPrinterJobResult result = await processor.ProcessAsync(job, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(VirtualPrinterJobStatus.Failed, result.Status);
        Assert.Contains("JobRouteResolved", listener.EventNames);
        Assert.Contains("JobFailed", listener.EventNames);
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

    /// <summary>
    /// Gets or sets the current MSTest context.
    /// </summary>
    /// <value>The current MSTest context.</value>
    public TestContext TestContext { get; set; } = null!;
}
