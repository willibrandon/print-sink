namespace PrintSink.Endpoints;

/// <summary>
/// Provides metadata for a sink write operation.
/// </summary>
public sealed class SinkWriteContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SinkWriteContext"/> class.
    /// </summary>
    /// <param name="endpoint">The virtual endpoint receiving the output.</param>
    /// <param name="contentType">The output content type.</param>
    /// <param name="jobName">The print job name when available.</param>
    public SinkWriteContext(VirtualEndpoint endpoint, string contentType, string? jobName)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Endpoint = endpoint;
        ContentType = contentType;
        JobName = jobName;
    }

    /// <summary>
    /// Gets the virtual endpoint receiving the output.
    /// </summary>
    public VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Gets the output content type.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the print job name when available.
    /// </summary>
    public string? JobName { get; }
}
