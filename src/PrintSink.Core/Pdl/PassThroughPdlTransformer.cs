using PrintSink.Core.Endpoints;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Pdl;

/// <summary>
/// Leaves PDL streams unchanged.
/// </summary>
public sealed class PassThroughPdlTransformer : IPdlTransformer
{
    private PassThroughPdlTransformer()
    {
    }

    /// <summary>
    /// Gets the shared pass-through transformer instance.
    /// </summary>
    public static PassThroughPdlTransformer Instance { get; } = new();

    /// <inheritdoc />
    public Task<Stream> TransformAsync(
        Stream source,
        VirtualEndpoint endpoint,
        PdlPlan plan,
        WatermarkOptions watermarkOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(watermarkOptions);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(source);
    }
}
