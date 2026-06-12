namespace PrintSink.Endpoints;

/// <summary>
/// Writes PDL bytes to a file path selected by the caller.
/// </summary>
public sealed class FileSink : ISink
{
  private readonly string path;

  /// <summary>
  /// Initializes a new instance of the <see cref="FileSink" /> class.
  /// </summary>
  /// <param name="path">The target file path.</param>
  public FileSink(string path)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    this.path = path;
  }

  /// <inheritdoc />
  public async ValueTask WriteAsync(Stream source, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(source);

    using FileStream output = new(
      path,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None,
      bufferSize: 81920,
      useAsync: true);

    await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
  }
}
