namespace PrintSink.Core.Endpoints;

/// <summary>
/// Writes PDL output to a target file.
/// </summary>
public sealed class FileSink : ISink
{
    /// <inheritdoc />
    public async Task WriteAsync(Stream pdl, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdl);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.TargetPath))
        {
            throw new InvalidOperationException("A file sink requires a target path.");
        }

        await using FileStream output = File.Create(context.TargetPath);
        await pdl.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
