namespace PrintSink.Endpoints;

/// <summary>
/// Uploads PDL content for a non-file virtual printer endpoint.
/// </summary>
public interface ICloudUploadClient
{
    /// <summary>
    /// Uploads transformed PDL content.
    /// </summary>
    /// <param name="pdlStream">The transformed PDL stream positioned at the beginning.</param>
    /// <param name="context">Metadata for the sink operation.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when upload finishes.</returns>
    Task UploadAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default);
}
