using PrintSink.Abstractions;
using PrintSink.Diagnostics;
using PrintSink.Endpoints;
using PrintSink.Pdl;
using PrintSink.Settings;
using PrintSink.Watermark;

namespace PrintSink.Processing;

/// <summary>
/// Orchestrates virtual printer job processing without depending on live WinRT activation types.
/// </summary>
public sealed class VirtualPrinterJobProcessor : IVirtualPrinterJobProcessor
{
    private readonly IPdlRouter router;
    private readonly IPdlConverter converter;
    private readonly IXpsWatermarkProcessor watermarkProcessor;
    private readonly WatermarkSettingsService watermarkSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobProcessor"/> class.
    /// </summary>
    /// <param name="router">The PDL router.</param>
    /// <param name="converter">The PDL converter adapter.</param>
    /// <param name="watermarkProcessor">The XPS watermark processor adapter.</param>
    /// <param name="watermarkSettingsService">The watermark settings service.</param>
    public VirtualPrinterJobProcessor(
        IPdlRouter router,
        IPdlConverter converter,
        IXpsWatermarkProcessor watermarkProcessor,
        WatermarkSettingsService watermarkSettingsService)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(watermarkProcessor);
        ArgumentNullException.ThrowIfNull(watermarkSettingsService);

        this.router = router;
        this.converter = converter;
        this.watermarkProcessor = watermarkProcessor;
        this.watermarkSettingsService = watermarkSettingsService;
    }

    /// <inheritdoc />
    public async Task<VirtualPrinterProcessingResult> ProcessAsync(IVirtualPrinterJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        PdlPlan? plan = null;

        try
        {
            PrintSinkEventSource.Log.JobStarted(job.Endpoint.Kind.ToString(), job.ContentType);

            WatermarkOptions watermarkOptions = await watermarkSettingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
            plan = router.Resolve(job.ContentType, job.Endpoint, watermarkOptions);
            PrintSinkEventSource.Log.PdlPlanResolved(plan);

            if (plan.Action == PdlActionKind.Reject)
            {
                return VirtualPrinterProcessingResult.Rejected(plan);
            }

            ISink sink = await job.OpenSinkAsync(cancellationToken).ConfigureAwait(false);
            Stream source = await job.OpenSourceAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                Stream sourceForAction = source;
                Stream? watermarkedSource = null;

                try
                {
                    if (plan.RequiresWatermark)
                    {
                        watermarkedSource = await watermarkProcessor.ApplyAsync(source, watermarkOptions, cancellationToken).ConfigureAwait(false);
                        sourceForAction = watermarkedSource;
                    }

                    using MemoryStream output = new();
                    await ExecutePlanAsync(plan, job, sourceForAction, output, cancellationToken).ConfigureAwait(false);
                    output.Position = 0;

                    SinkWriteContext context = new(job.Endpoint, plan.TargetContentType, job.JobName);
                    await sink.WriteAsync(output, context, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (watermarkedSource is not null)
                    {
                        await watermarkedSource.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }

            return VirtualPrinterProcessingResult.Succeeded(plan);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return VirtualPrinterProcessingResult.Canceled(plan);
        }
    }

    private async Task ExecutePlanAsync(
        PdlPlan plan,
        IVirtualPrinterJob job,
        Stream source,
        Stream output,
        CancellationToken cancellationToken)
    {
        switch (plan.Action)
        {
            case PdlActionKind.Copy:
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                break;
            case PdlActionKind.Convert:
                string printTicketXml = await job.GetPrintTicketXmlAsync(cancellationToken).ConfigureAwait(false);
                await converter.ConvertAsync(plan.Conversion, printTicketXml, source, output, cancellationToken).ConfigureAwait(false);
                break;
            case PdlActionKind.Reject:
                throw new InvalidOperationException("Rejected PDL plans cannot be executed.");
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, "Unsupported PDL action.");
        }
    }
}
