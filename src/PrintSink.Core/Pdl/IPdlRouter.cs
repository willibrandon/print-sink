namespace PrintSink.Pdl;

using PrintSink.Endpoints;

/// <summary>
/// Resolves a source PDL content type against a virtual endpoint.
/// </summary>
public interface IPdlRouter
{
  /// <summary>
  /// Creates a routing plan.
  /// </summary>
  /// <param name="contentType">The source content type.</param>
  /// <param name="endpoint">The destination endpoint.</param>
  /// <returns>The routing plan.</returns>
  PdlPlan Resolve(string contentType, VirtualEndpoint endpoint);
}
