using PrintSink.Core.Endpoints;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Leaves sink writes intentionally empty.
/// </summary>
internal sealed class NoOpSink : ISink
{
    /// <inheritdoc />
    public Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
