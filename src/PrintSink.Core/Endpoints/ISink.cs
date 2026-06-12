namespace PrintSink.Endpoints;

/// <summary>
/// Writes routed PDL to a destination.
/// </summary>
public interface ISink
{
  /// <summary>
  /// Writes the source stream.
  /// </summary>
  /// <param name="source">The source PDL stream.</param>
  /// <param name="cancellationToken">A token that cancels the write.</param>
  /// <returns>A task that completes when the write finishes.</returns>
  ValueTask WriteAsync(Stream source, CancellationToken cancellationToken);
}
