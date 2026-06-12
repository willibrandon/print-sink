namespace PrintSink.Endpoints;

using PrintSink.Pdl;

/// <summary>
/// Provides the built-in PrintSink endpoint definitions.
/// </summary>
public static class EndpointCatalog
{
    /// <summary>
    /// Gets the endpoints shown by the app and used by the router tests.
    /// </summary>
    public static IReadOnlyList<VirtualEndpoint> BuiltInQueues { get; } =
    [
        new(
            EndpointKind.Pdf,
            "PrintSink - PDF",
            "/pdf",
            "Save-As PDF",
            PdlFormat.Oxps,
            PdlFormat.Pdf,
            usesSaveAsDialog: true,
            [PdlFormat.Pdf],
            [".pdf"]),
        new(
            EndpointKind.Xps,
            "PrintSink - XPS",
            "/xps",
            "OXPS passthrough",
            PdlFormat.Oxps,
            PdlFormat.Oxps,
            usesSaveAsDialog: true,
            [PdlFormat.Oxps, PdlFormat.Xps],
            [".oxps", ".xps"]),
        new(
            EndpointKind.PostScript,
            "PrintSink - PostScript",
            "/postscript",
            "PostScript sink",
            PdlFormat.PostScript,
            PdlFormat.PostScript,
            usesSaveAsDialog: true,
            [PdlFormat.PostScript],
            [".ps"]),
        new(
            EndpointKind.Cloud,
            "PrintSink - Cloud",
            "/cloud",
            "No Save-As target",
            PdlFormat.Oxps,
            PdlFormat.Pdf,
            usesSaveAsDialog: false,
            [PdlFormat.Pdf],
            []),
        new(
            EndpointKind.PwgRaster,
            "PrintSink - PWG Raster",
            "/pwg",
            "Converter path",
            PdlFormat.Oxps,
            PdlFormat.PwgRaster,
            usesSaveAsDialog: true,
            [],
            [".pwg"]),
    ];

    /// <summary>
    /// Finds an endpoint by manifest path.
    /// </summary>
    /// <param name="endpointPath">The endpoint path, such as <c>/pdf</c>.</param>
    /// <returns>The matching endpoint.</returns>
    /// <exception cref="ArgumentException">No endpoint exists for <paramref name="endpointPath" />.</exception>
    public static VirtualEndpoint GetByPath(string endpointPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);

        foreach (VirtualEndpoint endpoint in BuiltInQueues)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(endpoint.EndpointPath, endpointPath))
            {
                return endpoint;
            }
        }

        throw new ArgumentException($"Unknown endpoint path '{endpointPath}'.", nameof(endpointPath));
    }
}
