using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;

namespace PrintSink.Core.Processing;

/// <summary>
/// Processes virtual printer jobs using testable Core abstractions.
/// </summary>
public sealed class VirtualPrinterJobProcessor
{
    private readonly IPdlRouter router;
    private readonly IPdlConverter converter;
    private readonly IEndpointSinkResolver sinkResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualPrinterJobProcessor"/> class.
    /// </summary>
    /// <param name="router">The PDL router.</param>
    /// <param name="converter">The PDL converter.</param>
    /// <param name="sinkResolver">The endpoint sink resolver.</param>
    public VirtualPrinterJobProcessor(IPdlRouter router, IPdlConverter converter, IEndpointSinkResolver sinkResolver)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(sinkResolver);

        this.router = router;
        this.converter = converter;
        this.sinkResolver = sinkResolver;
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

        PdlPlan plan = router.Resolve(job.ContentType, job.Endpoint);
        if (plan.ActionKind == PdlActionKind.Reject)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, cancellationToken).ConfigureAwait(false);
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Failed, null);
        }

        try
        {
            await ProcessAcceptedJobAsync(job, plan, cancellationToken).ConfigureAwait(false);
            await job.CompleteAsync(VirtualPrinterJobStatus.Succeeded, cancellationToken).ConfigureAwait(false);

            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Succeeded, null);
        }
        catch (OperationCanceledException ex)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Canceled, CancellationToken.None).ConfigureAwait(false);
            return new VirtualPrinterJobResult(plan, VirtualPrinterJobStatus.Canceled, ex);
        }
        catch (Exception ex)
        {
            await job.CompleteAsync(VirtualPrinterJobStatus.Failed, CancellationToken.None).ConfigureAwait(false);
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

                converted = await converter.ConvertAsync(source, conversionKind, cancellationToken).ConfigureAwait(false);
                output = converted;
            }

            ISink sink = sinkResolver.Resolve(job.Endpoint);
            SinkWriteContext context = new(
                job.Endpoint,
                PdlFormatInfo.GetContentType(plan.TargetFormat),
                null,
                target);

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
}
