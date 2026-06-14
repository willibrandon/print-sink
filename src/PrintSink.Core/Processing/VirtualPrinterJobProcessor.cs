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
    private readonly IPdlTransformer transformer;
    private readonly IEndpointSinkResolver sinkResolver;
    private readonly ISettingsStore? settingsStore;
    private readonly JobProcessingOptions? jobProcessingOptions;
    private readonly IDiagnosticEventStore? diagnosticEventStore;

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
        : this(
            router,
            converter,
            sinkResolver,
            settingsStore,
            jobProcessingOptions,
            PassThroughPdlTransformer.Instance)
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
    /// <param name="transformer">The PDL transformer applied before conversion or sink writes.</param>
    public VirtualPrinterJobProcessor(
        IPdlRouter router,
        IPdlConverter converter,
        IEndpointSinkResolver sinkResolver,
        ISettingsStore? settingsStore,
        JobProcessingOptions? jobProcessingOptions,
        IPdlTransformer transformer)
        : this(router, converter, sinkResolver, settingsStore, jobProcessingOptions, transformer, null)
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
    /// <param name="transformer">The PDL transformer applied before conversion or sink writes.</param>
    /// <param name="diagnosticEventStore">The store used to persist recent diagnostics.</param>
    public VirtualPrinterJobProcessor(
        IPdlRouter router,
        IPdlConverter converter,
        IEndpointSinkResolver sinkResolver,
        ISettingsStore? settingsStore,
        JobProcessingOptions? jobProcessingOptions,
        IPdlTransformer transformer,
        IDiagnosticEventStore? diagnosticEventStore)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(sinkResolver);
        ArgumentNullException.ThrowIfNull(transformer);

        this.router = router;
        this.converter = converter;
        this.transformer = transformer;
        this.sinkResolver = sinkResolver;
        this.settingsStore = settingsStore;
        this.jobProcessingOptions = jobProcessingOptions;
        this.diagnosticEventStore = diagnosticEventStore;
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
        string routeDetail = FormatRouteDetail(job.ContentType, plan);
        PrintSinkDiagnostics.Log.JobRouteResolved(
            job.Endpoint.QueueName,
            job.ContentType,
            plan.ActionKind.ToString(),
            plan.SourceFormat?.ToString() ?? "Unknown",
            plan.TargetFormat.ToString(),
            plan.ConversionKind?.ToString() ?? "None",
            plan.Reason);
        await RecordDiagnosticEventAsync(
            new DiagnosticEventRecord(
                DateTimeOffset.UtcNow,
                DiagnosticEventSeverity.Information,
                nameof(VirtualPrinterJobProcessor),
                "Route resolved",
                job.Endpoint.QueueName,
                routeDetail),
            cancellationToken)
            .ConfigureAwait(false);

        if (plan.ActionKind == PdlActionKind.Reject)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, cancellationToken).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobRejected(
                job.Endpoint.QueueName,
                plan.Reason,
                GetElapsedMilliseconds(started));
            await RecordDiagnosticEventAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.UtcNow,
                    DiagnosticEventSeverity.Warning,
                    nameof(VirtualPrinterJobProcessor),
                    "Job rejected",
                    job.Endpoint.QueueName,
                    plan.Reason),
                CancellationToken.None)
                .ConfigureAwait(false);
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Failed, null);
        }

        try
        {
            await ProcessAcceptedJobAsync(job, plan, cancellationToken).ConfigureAwait(false);
            await RecordDiagnosticEventAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.UtcNow,
                    DiagnosticEventSeverity.Information,
                    nameof(VirtualPrinterJobProcessor),
                    "Job completion requested",
                    job.Endpoint.QueueName,
                    routeDetail),
                CancellationToken.None)
                .ConfigureAwait(false);
            await job.CompleteAsync(VirtualPrinterJobStatus.Succeeded, cancellationToken).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobCompleted(
                job.Endpoint.QueueName,
                VirtualPrinterJobStatus.Succeeded.ToString(),
                GetElapsedMilliseconds(started));
            await RecordDiagnosticEventAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.UtcNow,
                    DiagnosticEventSeverity.Information,
                    nameof(VirtualPrinterJobProcessor),
                    "Job completed",
                    job.Endpoint.QueueName,
                    $"{VirtualPrinterJobStatus.Succeeded}; {GetElapsedMilliseconds(started)} ms; route={routeDetail}; {FormatJobPasswordDetail()}"),
                CancellationToken.None)
                .ConfigureAwait(false);

            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Succeeded, null);
        }
        catch (OperationCanceledException ex)
        {
            string exceptionType = ex.GetType().FullName ?? ex.GetType().Name;
            string exceptionDetail = FormatExceptionDetail(ex);
            await job.CompleteAsync(VirtualPrinterJobStatus.Canceled, CancellationToken.None).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobFailed(
                job.Endpoint.QueueName,
                exceptionType,
                exceptionDetail,
                GetElapsedMilliseconds(started));
            await RecordDiagnosticEventAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.UtcNow,
                    DiagnosticEventSeverity.Warning,
                    nameof(VirtualPrinterJobProcessor),
                    "Job canceled",
                    job.Endpoint.QueueName,
                    $"{exceptionDetail}; route={routeDetail}"),
                CancellationToken.None)
                .ConfigureAwait(false);
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Canceled, ex);
        }
        catch (Exception ex) when (IsPrintJobFailure(ex))
        {
            string exceptionType = ex.GetType().FullName ?? ex.GetType().Name;
            string exceptionDetail = FormatExceptionDetail(ex);
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, CancellationToken.None).ConfigureAwait(false);
            PrintSinkDiagnostics.Log.JobFailed(
                job.Endpoint.QueueName,
                exceptionType,
                exceptionDetail,
                GetElapsedMilliseconds(started));
            await RecordDiagnosticEventAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.UtcNow,
                    DiagnosticEventSeverity.Error,
                    nameof(VirtualPrinterJobProcessor),
                    "Job failed",
                    job.Endpoint.QueueName,
                    $"{exceptionDetail}; route={routeDetail}"),
                CancellationToken.None)
                .ConfigureAwait(false);
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Failed, ex);
        }
    }

    private async Task ProcessAcceptedJobAsync(
        IVirtualPrinterJob job,
        PdlPlan plan,
        CancellationToken cancellationToken)
    {
        await RecordDiagnosticEventAsync(
            new DiagnosticEventRecord(
                DateTimeOffset.UtcNow,
                DiagnosticEventSeverity.Information,
                nameof(VirtualPrinterJobProcessor),
                "Job source opening",
                job.Endpoint.QueueName,
                job.ContentType),
            cancellationToken)
            .ConfigureAwait(false);
        Stream source = await job.OpenSourceAsync(cancellationToken).ConfigureAwait(false);
        await RecordDiagnosticEventAsync(
            new DiagnosticEventRecord(
                DateTimeOffset.UtcNow,
                DiagnosticEventSeverity.Information,
                nameof(VirtualPrinterJobProcessor),
                "Job source opened",
                job.Endpoint.QueueName,
                job.ContentType),
            cancellationToken)
            .ConfigureAwait(false);

        await RecordDiagnosticEventAsync(
            new DiagnosticEventRecord(
                DateTimeOffset.UtcNow,
                DiagnosticEventSeverity.Information,
                nameof(VirtualPrinterJobProcessor),
                "Job target opening",
                job.Endpoint.QueueName,
                job.Endpoint.RequiresTargetFile ? "target=file" : "target=sink"),
            cancellationToken)
            .ConfigureAwait(false);
        Stream? target = await job.OpenTargetAsync(cancellationToken).ConfigureAwait(false);
        await RecordDiagnosticEventAsync(
            new DiagnosticEventRecord(
                DateTimeOffset.UtcNow,
                DiagnosticEventSeverity.Information,
                nameof(VirtualPrinterJobProcessor),
                "Job target opened",
                job.Endpoint.QueueName,
                target is null ? "target=sink" : "target=file"),
            cancellationToken)
            .ConfigureAwait(false);

        await using (source.ConfigureAwait(false))
        {
            WatermarkOptions watermarkOptions = await GetWatermarkOptionsAsync(job.Endpoint, cancellationToken)
                .ConfigureAwait(false);
            Stream transformed = await transformer
                .TransformAsync(source, job.Endpoint, plan, watermarkOptions, cancellationToken)
                .ConfigureAwait(false);
            RewindIfSeekable(transformed);
            Stream output = transformed;
            Stream? transformedToDispose = ReferenceEquals(source, transformed) ? null : transformed;
            Stream? converted = null;

            try
            {
                if (plan.ActionKind == PdlActionKind.Convert)
                {
                    PdlConversionKind conversionKind = plan.ConversionKind
                        ?? throw new InvalidOperationException("A conversion plan must include a conversion kind.");

                    long conversionStarted = Stopwatch.GetTimestamp();
                    PrintSinkDiagnostics.Log.PdlConversionStarted(job.Endpoint.QueueName, conversionKind.ToString());
                    converted = await converter.ConvertAsync(output, conversionKind, cancellationToken).ConfigureAwait(false);
                    RewindIfSeekable(converted);
                    PrintSinkDiagnostics.Log.PdlConversionCompleted(
                        job.Endpoint.QueueName,
                        conversionKind.ToString(),
                        GetElapsedMilliseconds(conversionStarted));
                    output = converted;
                }

                ISink sink = sinkResolver.Resolve(job.Endpoint);
                SinkWriteContext context = new(
                    job.Endpoint,
                    PdlFormatInfo.GetContentType(plan.TargetFormat),
                    null,
                    target,
                    watermarkOptions);

                EnsureNonEmptyOutput(output, job.Endpoint);
                await sink.WriteAsync(output, context, cancellationToken).ConfigureAwait(false);
                if (target is not null)
                {
                    await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                    EnsureNonEmptyTarget(target, job.Endpoint);
                }

                await RecordDiagnosticEventAsync(
                    new DiagnosticEventRecord(
                        DateTimeOffset.UtcNow,
                        DiagnosticEventSeverity.Information,
                        nameof(VirtualPrinterJobProcessor),
                        "Job sink write completed",
                        job.Endpoint.QueueName,
                        $"targetFormat={plan.TargetFormat}; bytes={GetWrittenByteCount(output, target)}"),
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (transformedToDispose is not null)
                {
                    await transformedToDispose.DisposeAsync().ConfigureAwait(false);
                }

                if (converted is not null)
                {
                    await converted.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private static void EnsureNonEmptyOutput(Stream output, VirtualEndpoint endpoint)
    {
        if (endpoint.RequiresTargetFile && output.CanSeek && output.Length == 0)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint.QueueName}' produced empty {endpoint.TargetFormat} output.");
        }
    }

    private static void EnsureNonEmptyTarget(Stream target, VirtualEndpoint endpoint)
    {
        if (endpoint.RequiresTargetFile && target.CanSeek && target.Length == 0)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint.QueueName}' target stream is empty after sink write.");
        }
    }

    private static string GetWrittenByteCount(Stream output, Stream? target)
    {
        if (target is not null && target.CanSeek)
        {
            return target.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return output.CanSeek
            ? output.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "unknown";
    }

    private static long GetElapsedMilliseconds(long started)
    {
        return (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private static string FormatRouteDetail(string contentType, PdlPlan plan)
    {
        return $"{contentType} -> {plan.TargetFormat}; {plan.ActionKind}; {plan.Reason}";
    }

    private static string FormatExceptionDetail(Exception exception)
    {
        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        string hresult = $"0x{exception.HResult:X8}";
        return string.IsNullOrWhiteSpace(exception.Message)
            ? $"{exceptionType} ({hresult})"
            : $"{exceptionType} ({hresult}): {exception.Message}";
    }

    private string FormatJobPasswordDetail()
    {
        return jobProcessingOptions?.JobPasswordOptions is null
            ? "job-password=absent"
            : "job-password=present-not-applicable";
    }

    private static void RewindIfSeekable(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
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

    private async Task RecordDiagnosticEventAsync(
        DiagnosticEventRecord record,
        CancellationToken cancellationToken)
    {
        if (diagnosticEventStore is null)
        {
            return;
        }

        try
        {
            await diagnosticEventStore.AppendAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsDiagnosticPersistenceFailure(ex))
        {
            // Diagnostics persistence must never fail print-job processing.
        }
    }

    private static bool IsPrintJobFailure(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException;
    }

    private static bool IsDiagnosticPersistenceFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException
            or TimeoutException
            or OperationCanceledException;
    }
}
