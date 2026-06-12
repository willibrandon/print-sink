namespace PrintSink.Endpoints;

/// <summary>
/// Copies PDL output to a target file stream supplied by the Save As broker.
/// </summary>
public sealed class FileSink : ISink
{
    private readonly Stream targetStream;
    private readonly bool leaveOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSink"/> class.
    /// </summary>
    /// <param name="targetStream">The target file stream.</param>
    /// <param name="leaveOpen">Whether to leave the target stream open after writing.</param>
    public FileSink(Stream targetStream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(targetStream);

        this.targetStream = targetStream;
        this.leaveOpen = leaveOpen;
    }

    /// <inheritdoc />
    public async Task WriteAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdlStream);
        ArgumentNullException.ThrowIfNull(context);

        await pdlStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
        await targetStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (!leaveOpen)
        {
            await targetStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
