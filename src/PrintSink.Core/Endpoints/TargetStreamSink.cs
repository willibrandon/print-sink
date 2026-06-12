namespace PrintSink.Core.Endpoints;

/// <summary>
/// Writes PDL output to the target stream supplied by a virtual printer job.
/// </summary>
public sealed class TargetStreamSink : ISink
{
    /// <inheritdoc />
    public async Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);

        if (context.TargetStream is null)
        {
            throw new InvalidOperationException("A target-stream sink requires a target stream.");
        }

        await pdl.CopyToAsync(context.TargetStream, cancellationToken).ConfigureAwait(false);
    }
}
