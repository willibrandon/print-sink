namespace PrintSink.Endpoints;

/// <summary>
/// Sends PDL output to a custom non-file cloud client.
/// </summary>
public sealed class CloudSink : ISink
{
    private readonly ICloudUploadClient uploadClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudSink"/> class.
    /// </summary>
    /// <param name="uploadClient">The upload client.</param>
    public CloudSink(ICloudUploadClient uploadClient)
    {
        ArgumentNullException.ThrowIfNull(uploadClient);

        this.uploadClient = uploadClient;
    }

    /// <inheritdoc />
    public Task WriteAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdlStream);
        ArgumentNullException.ThrowIfNull(context);

        return uploadClient.UploadAsync(pdlStream, context, cancellationToken);
    }
}
