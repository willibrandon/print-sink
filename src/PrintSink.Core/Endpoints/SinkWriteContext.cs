namespace PrintSink.Core.Endpoints;

/// <summary>
/// Describes a sink write operation.
/// </summary>
public sealed class SinkWriteContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SinkWriteContext"/> class.
    /// </summary>
    /// <param name="endpoint">The target endpoint.</param>
    /// <param name="contentType">The content type being written.</param>
    /// <param name="targetPath">The target file path, when the endpoint writes to a file path.</param>
    public SinkWriteContext(VirtualEndpoint endpoint, string contentType, string? targetPath)
        : this(endpoint, contentType, targetPath, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SinkWriteContext"/> class.
    /// </summary>
    /// <param name="endpoint">The target endpoint.</param>
    /// <param name="contentType">The content type being written.</param>
    /// <param name="targetPath">The target file path, when the endpoint writes to a file path.</param>
    /// <param name="targetStream">The target stream, when the endpoint writes to an OS-provided stream.</param>
    public SinkWriteContext(VirtualEndpoint endpoint, string contentType, string? targetPath, Stream? targetStream)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Endpoint = endpoint;
        ContentType = contentType;
        TargetPath = targetPath;
        TargetStream = targetStream;
    }

    /// <summary>
    /// Gets the target endpoint.
    /// </summary>
    public VirtualEndpoint Endpoint { get; }

    /// <summary>
    /// Gets the content type being written.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the target file path, when the endpoint writes to a file path.
    /// </summary>
    public string? TargetPath { get; }

    /// <summary>
    /// Gets the target stream, when the endpoint writes to an OS-provided stream.
    /// </summary>
    public Stream? TargetStream { get; }
}
