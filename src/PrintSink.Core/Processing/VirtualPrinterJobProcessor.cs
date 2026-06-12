using System.Diagnostics;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Processing;

/// <summary>
/// Processes virtual printer jobs using testable Core abstractions.
/// </summary>
public sealed class VirtualPrinterJobProcessor
{
    private readonly IPdlRouter router;
    private readonly IPdlConverter converter;
    private readonly IEndpointSinkResolver sinkResolver;
    private readonly ISettingsStore? settingsStore;
    private readonly JobProcessingOptions? jobProcessingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobProcessor"/> class.
    /// </summary>
    /// <param name="router">The PDL router.</param>
    /// <param name="converter">The PDL converter.</param>
    /// <param name="sinkResolver">The endpoint sink resolver.</param>
    public VirtualPrinterJobProcessor(IPdlRouter router, IPdlConverter converter, IEndpointSinkResolver sinkResolver)
        : this(router, converter, sinkResolver, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobProcessor"/> class.
    /// </summary>
    /// <param name="router">The PDL router.</param>
    /// <param name="converter">The PDL converter.</param>
    /// <param name="sinkResolver">The endpoint sink resolver.</param>
    /// <param name="settingsStore">The settings store used to load job options.</param>
    public VirtualPrinterJobProcessor(
        IPdlRouter router,
        IPdlConverter converter,
        IEndpointSinkResolver sinkResolver,
        ISettingsStore? settingsStore)
        : this(router, converter, sinkResolver, settingsStore, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobProcessor"/> class.
    /// </summary>
    /// <param name="router">The PDL router.</param>
    /// <param name="converter">The PDL converter.</param>
    /// <param name="sinkResolver">The endpoint sink resolver.</param>
    /// <param name="settingsStore">The settings store used to load endpoint options.</param>
    /// <param name="jobProcessingOptions">The foreground job options, when job UI collected any.</param>
    public VirtualPrinterJobProcessor(
        IPdlRouter router,
        IPdlConverter converter,
        IEndpointSinkResolver sinkResolver,
        ISettingsStore? settingsStore,
        JobProcessingOptions? jobProcessingOptions)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(sinkResolver);

        this.router = router;
        this.converter = converter;
        this.sinkResolver = sinkResolver;
        this.settingsStore = settingsStore;
        this.jobProcessingOptions = jobProcessingOptions;
    }

    /// <summary>
    /// Processes a virtual printer job.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">A token that cancels processing.</param>
    /// <returns>The job processing result.</returns>
    public async Task<VirtualPrinterJobResult> ProcessAsync(
        IVirtualPrinterJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        long started = Stopwatch.GetTimestamp();
        PdlPlan plan = router.Resolve(job.ContentType, job.Endpoint);
        PrintSinkDiagnostics.Log.JobRouteResolved(
            job.Endpoint.QueueName,
            job.ContentType,
            plan.ActionKind.ToString(),
            plan.SourceFormat?.ToString() ?? "Unknown",
            plan.TargetFormat.ToString(),
            plan.ConversionKind?.ToString() ?? "None",
            plan.Reason);

        if (plan.ActionKind == PdlActionKind.Reject)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, cancellationToken).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobRejected(
                job.Endpoint.QueueName,
                plan.Reason,
                GetElapsedMilliseconds(started));
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Failed, null);
        }

        try
        {
            await ProcessAcceptedJobAsync(job, plan, cancellationToken).ConfigureAwait(false);
            await job.CompleteAsync(VirtualPrinterJobStatus.Succeeded, cancellationToken).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobCompleted(
                job.Endpoint.QueueName,
                VirtualPrinterJobStatus.Succeeded.ToString(),
                GetElapsedMilliseconds(started));

            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Succeeded, null);
        }
        catch (OperationCanceledException ex)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Canceled, CancellationToken.None).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobFailed(
                job.Endpoint.QueueName,
                ex.GetType().FullName ?? ex.GetType().Name,
                ex.Message,
                GetElapsedMilliseconds(started));
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Canceled, ex);
        }
        catch (Exception ex)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, CancellationToken.None).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobFailed(
                job.Endpoint.QueueName,
                ex.GetType().FullName ?? ex.GetType().Name,
                ex.Message,
                GetElapsedMilliseconds(started));
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Failed, ex);
        }
    }

    private async Task ProcessAcceptedJobAsync(
        IVirtualPrinterJob job,
        PdlPlan plan,
        CancellationToken cancellationToken)
    {
        await using Stream source = await job.OpenSourceAsync(cancellationToken).ConfigureAwait(false);
        await using Stream? target = await job.OpenTargetAsync(cancellationToken).ConfigureAwait(false);

        Stream output = source;
        Stream? converted = null;

        try
        {
            if (plan.ActionKind == PdlActionKind.Convert)
            {
                PdlConversionKind conversionKind = plan.ConversionKind
                    ?? throw new InvalidOperationException("A conversion plan must include a conversion kind.");

                long conversionStarted = Stopwatch.GetTimestamp();
                PrintSinkDiagnostics.Log.PdlConversionStarted(job.Endpoint.QueueName, conversionKind.ToString());
                converted = await converter.ConvertAsync(source, conversionKind, cancellationToken).ConfigureAwait(false);
                PrintSinkDiagnostics.Log.PdlConversionCompleted(
                    job.Endpoint.QueueName,
                    conversionKind.ToString(),
                    GetElapsedMilliseconds(conversionStarted));
                output = converted;
            }

            ISink sink = sinkResolver.Resolve(job.Endpoint);
            WatermarkOptions watermarkOptions = await GetWatermarkOptionsAsync(job.Endpoint, cancellationToken)
                .ConfigureAwait(false);
            SinkWriteContext context = new(
                job.Endpoint,
                PdlFormatInfo.GetContentType(plan.TargetFormat),
                null,
                target,
                watermarkOptions);

            await sink.WriteAsync(output, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (converted is not null)
            {
                await converted.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static long GetElapsedMilliseconds(long started)
    {
        return (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private async Task<WatermarkOptions> GetWatermarkOptionsAsync(
        VirtualEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (jobProcessingOptions is not null)
        {
            return jobProcessingOptions.WatermarkOptions;
        }

        if (settingsStore is null)
        {
            return WatermarkOptions.Disabled;
        }

        return await settingsStore
            .GetWatermarkOptionsAsync(endpoint.PrinterUri, cancellationToken)
            .ConfigureAwait(false);
    }
}
