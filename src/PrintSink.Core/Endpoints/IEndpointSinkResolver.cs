namespace PrintSink.Core.Endpoints;

/// <summary>
/// Resolves a sink for a virtual endpoint.
/// </summary>
public interface IEndpointSinkResolver
{
    /// <summary>
    /// Resolves a sink for the supplied endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint that will receive output.</param>
    /// <returns>The sink for the endpoint.</returns>
    ISink Resolve(VirtualEndpoint endpoint);
}
