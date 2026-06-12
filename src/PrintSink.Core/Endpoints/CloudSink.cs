namespace PrintSink.Endpoints;

/// <summary>
/// Placeholder sink for endpoints that do not write to a local Save As target.
/// </summary>
public sealed class CloudSink : ISink
{
    /// <inheritdoc />
    public async ValueTask WriteAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await source.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
    }
}
