using System.Collections.ObjectModel;
using PrintSink.Pdl;

namespace PrintSink.Endpoints;

/// <summary>
/// Provides the built-in virtual endpoints that the package manifest will declare.
/// </summary>
public static class EndpointCatalog
{
    /// <summary>
    /// Gets the built-in PDF endpoint.
    /// </summary>
    public static VirtualEndpoint Pdf { get; } = new(
        EndpointKind.PdfFile,
        "PrinterPdfDisplayName",
        "/pdf",
        PdlFormat.Oxps,
        PdlFormat.Pdf,
        usesSaveAsDialog: true,
        supportedPassthroughFormats: new[] { PdlFormat.Pdf },
        outputFileExtensions: new[] { ".pdf" });

    /// <summary>
    /// Gets the built-in XPS endpoint.
    /// </summary>
    public static VirtualEndpoint Xps { get; } = new(
        EndpointKind.XpsFile,
        "PrinterXpsDisplayName",
        "/xps",
        PdlFormat.Oxps,
        PdlFormat.Xps,
        usesSaveAsDialog: true,
        supportedPassthroughFormats: new[] { PdlFormat.Oxps, PdlFormat.Xps },
        outputFileExtensions: new[] { ".oxps", ".xps" });

    /// <summary>
    /// Gets the built-in PostScript endpoint.
    /// </summary>
    public static VirtualEndpoint PostScript { get; } = new(
        EndpointKind.PostScriptFile,
        "PrinterPostScriptDisplayName",
        "/postscript",
        PdlFormat.PostScript,
        PdlFormat.PostScript,
        usesSaveAsDialog: true,
        supportedPassthroughFormats: new[] { PdlFormat.PostScript },
        outputFileExtensions: new[] { ".ps" });

    /// <summary>
    /// Gets the built-in cloud endpoint.
    /// </summary>
    public static VirtualEndpoint Cloud { get; } = new(
        EndpointKind.Cloud,
        "PrinterCloudDisplayName",
        "/cloud",
        PdlFormat.Oxps,
        PdlFormat.Pdf,
        usesSaveAsDialog: false,
        supportedPassthroughFormats: new[] { PdlFormat.Pdf },
        outputFileExtensions: Array.Empty<string>());

    /// <summary>
    /// Gets the built-in PWG Raster endpoint.
    /// </summary>
    public static VirtualEndpoint PwgRaster { get; } = new(
        EndpointKind.PwgRasterFile,
        "PrinterPwgRasterDisplayName",
        "/pwg-raster",
        PdlFormat.Oxps,
        PdlFormat.PwgRaster,
        usesSaveAsDialog: true,
        supportedPassthroughFormats: new[] { PdlFormat.PwgRaster },
        outputFileExtensions: new[] { ".pwg" });

    /// <summary>
    /// Gets a custom-file PCLm endpoint that exercises the XPS-to-PCLm conversion path.
    /// </summary>
    public static VirtualEndpoint Pclm { get; } = new(
        EndpointKind.PclmFile,
        "PrinterPclmDisplayName",
        "/pclm",
        PdlFormat.Oxps,
        PdlFormat.Pclm,
        usesSaveAsDialog: true,
        supportedPassthroughFormats: new[] { PdlFormat.Pclm },
        outputFileExtensions: new[] { ".pclm" });

    /// <summary>
    /// Gets the five queues called out by the approved design.
    /// </summary>
    public static IReadOnlyList<VirtualEndpoint> BuiltInQueues { get; } = new ReadOnlyCollection<VirtualEndpoint>(
        new[] { Pdf, Xps, PostScript, Cloud, PwgRaster });

    /// <summary>
    /// Gets all endpoint shapes supported by the core router, including optional custom-file targets.
    /// </summary>
    public static IReadOnlyList<VirtualEndpoint> SupportedEndpoints { get; } = new ReadOnlyCollection<VirtualEndpoint>(
        new[] { Pdf, Xps, PostScript, Cloud, PwgRaster, Pclm });

    /// <summary>
    /// Resolves an endpoint by its printer address path.
    /// </summary>
    /// <param name="endpointPath">The address path reported by the print device.</param>
    /// <returns>The matching endpoint.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no endpoint matches the path.</exception>
    public static VirtualEndpoint FromEndpointPath(string endpointPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);

        string normalized = endpointPath.StartsWith('/') ? endpointPath : "/" + endpointPath;
        VirtualEndpoint? endpoint = SupportedEndpoints.FirstOrDefault(candidate =>
            string.Equals(candidate.EndpointPath, normalized, StringComparison.OrdinalIgnoreCase));

        return endpoint ?? throw new KeyNotFoundException($"No PrintSink endpoint is registered for address path '{endpointPath}'.");
    }
}
