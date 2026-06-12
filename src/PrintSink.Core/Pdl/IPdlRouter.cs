using PrintSink.Endpoints;
using PrintSink.Watermark;

namespace PrintSink.Pdl;

/// <summary>
/// Resolves the copy, conversion, or rejection plan for a virtual printer job.
/// </summary>
public interface IPdlRouter
{
    /// <summary>
    /// Resolves the processing plan for the given source content type and endpoint.
    /// </summary>
    /// <param name="contentType">The source PDL content type reported by the print workflow.</param>
    /// <param name="endpoint">The virtual endpoint selected by the print job.</param>
    /// <param name="watermarkOptions">The effective watermark options for the job.</param>
    /// <returns>The deterministic PDL processing plan.</returns>
    PdlPlan Resolve(string contentType, VirtualEndpoint endpoint, WatermarkOptions? watermarkOptions = null);
}
