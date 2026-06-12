namespace PrintSink.Core.Endpoints;

/// <summary>
/// Adapts a custom cloud upload callback to the sink contract.
/// </summary>
public sealed class CloudSink : ISink
{
    private readonly Func<Stream, SinkWriteContext, CancellationToken, Task> writeAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudSink"/> class.
    /// </summary>
    /// <param name="writeAsync">The callback that uploads a PDL stream.</param>
    public CloudSink(Func<Stream, SinkWriteContext, CancellationToken, Task> writeAsync)
    {
        ArgumentNullException.ThrowIfNull(writeAsync);

        this.writeAsync = writeAsync;
    }

    /// <inheritdoc />
    public Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);

        return writeAsync(pdl, context, cancellationToken);
    }
}
