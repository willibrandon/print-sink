namespace PrintSink.Core.Endpoints;

/// <summary>
/// Writes a transformed PDL stream to a destination.
/// </summary>
public interface ISink
{
    /// <summary>
    /// Writes a PDL stream.
    /// </summary>
    /// <param name="pdl">The PDL stream to write.</param>
    /// <param name="context">The sink write context.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the write is finished.</returns>
    Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default);
}
