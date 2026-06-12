using PrintSink.Core.Endpoints;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Pdl;

/// <summary>
/// Transforms accepted PDL streams before conversion or sink writes.
/// </summary>
public interface IPdlTransformer
{
    /// <summary>
    /// Transforms an accepted PDL stream.
    /// </summary>
    /// <param name="source">The source PDL stream positioned at the beginning.</param>
    /// <param name="endpoint">The endpoint receiving the job.</param>
    /// <param name="plan">The resolved PDL processing plan.</param>
    /// <param name="watermarkOptions">The watermark options for the job.</param>
    /// <param name="cancellationToken">A token that cancels transformation.</param>
    /// <returns>
    /// The transformed stream positioned at the beginning. Implementations may return
    /// <paramref name="source" /> unchanged when no transformation is required.
    /// </returns>
    Task<Stream> TransformAsync(
        Stream source,
        VirtualEndpoint endpoint,
        PdlPlan plan,
        WatermarkOptions watermarkOptions,
        CancellationToken cancellationToken = default);
}
