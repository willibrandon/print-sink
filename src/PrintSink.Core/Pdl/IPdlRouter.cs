using PrintSink.Core.Endpoints;

namespace PrintSink.Core.Pdl;

/// <summary>
/// Resolves PDL copy, conversion, and rejection decisions.
/// </summary>
public interface IPdlRouter
{
    /// <summary>
    /// Resolves the route for a source content type and endpoint.
    /// </summary>
    /// <param name="contentType">The source stream content type.</param>
    /// <param name="endpoint">The target virtual endpoint.</param>
    /// <returns>The selected PDL plan.</returns>
    PdlPlan Resolve(string contentType, VirtualEndpoint endpoint);
}
