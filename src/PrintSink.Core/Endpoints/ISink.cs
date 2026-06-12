namespace PrintSink.Endpoints;

/// <summary>
/// Writes transformed PDL content to a virtual printer sink.
/// </summary>
public interface ISink
{
    /// <summary>
    /// Writes a PDL stream to the sink.
    /// </summary>
    /// <param name="pdlStream">The transformed PDL stream positioned at the beginning.</param>
    /// <param name="context">Metadata for the sink operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the sink write finishes.</returns>
    Task WriteAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default);
}
