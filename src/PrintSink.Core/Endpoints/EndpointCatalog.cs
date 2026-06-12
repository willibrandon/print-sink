using PrintSink.Core.Pdl;

namespace PrintSink.Core.Endpoints;

/// <summary>
/// Provides the built-in PrintSink virtual endpoints.
/// </summary>
public static class EndpointCatalog
{
    private static readonly VirtualEndpoint[] Endpoints =
    [
        new(
            EndpointKind.Pdf,
            "PrintSink - PDF",
            new Uri("ipp://localhost/printsink/pdf"),
            PdlFormat.Oxps,
            PdlFormat.Pdf,
            [PdlFormat.Pdf],
            true,
            ".pdf"),
        new(
            EndpointKind.Xps,
            "PrintSink - XPS",
            new Uri("ipp://localhost/printsink/xps"),
            PdlFormat.Oxps,
            PdlFormat.Oxps,
            [PdlFormat.Oxps, PdlFormat.Xps],
            true,
            ".oxps"),
        new(
            EndpointKind.PostScript,
            "PrintSink - PostScript",
            new Uri("ipp://localhost/printsink/postscript"),
            PdlFormat.PostScript,
            PdlFormat.PostScript,
            [PdlFormat.PostScript],
            true,
            ".ps"),
        new(
            EndpointKind.Cloud,
            "PrintSink - Cloud",
            new Uri("ipp://localhost/printsink/cloud"),
            PdlFormat.Oxps,
            PdlFormat.Pdf,
            [PdlFormat.Pdf],
            false,
            null),
        new(
            EndpointKind.PwgRaster,
            "PrintSink - PWG Raster",
            new Uri("ipp://localhost/printsink/pwg-raster"),
            PdlFormat.Oxps,
            PdlFormat.PwgRaster,
            [],
            true,
            ".pwg"),
    ];

    /// <summary>
    /// Gets all built-in endpoints in display order.
    /// </summary>
    public static IReadOnlyList<VirtualEndpoint> All { get; } = Endpoints;

    /// <summary>
    /// Gets an endpoint by kind.
    /// </summary>
    /// <param name="kind">The endpoint kind.</param>
    /// <returns>The matching endpoint.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the endpoint kind is not registered.</exception>
    public static VirtualEndpoint GetByKind(EndpointKind kind)
    {
        return TryGetByKind(kind, out VirtualEndpoint? endpoint)
            ? endpoint!
            : throw new InvalidOperationException($"Endpoint '{kind}' is not registered.");
    }

    /// <summary>
    /// Tries to get an endpoint by kind.
    /// </summary>
    /// <param name="kind">The endpoint kind.</param>
    /// <param name="endpoint">The matching endpoint when found.</param>
    /// <returns><see langword="true"/> when the endpoint exists; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetByKind(EndpointKind kind, out VirtualEndpoint? endpoint)
    {
        endpoint = Endpoints.FirstOrDefault(candidate => candidate.Kind == kind);

        return endpoint is not null;
    }

    /// <summary>
    /// Tries to resolve an endpoint from a printer URI.
    /// </summary>
    /// <param name="printerUri">The printer URI reported by the print system.</param>
    /// <param name="endpoint">The matching endpoint when found.</param>
    /// <returns><see langword="true"/> when the endpoint exists; otherwise, <see langword="false"/>.</returns>
    public static bool TryResolve(Uri printerUri, out VirtualEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(printerUri);

        endpoint = Endpoints.FirstOrDefault(candidate => UriEquals(candidate.PrinterUri, printerUri));

        return endpoint is not null;
    }

    private static bool UriEquals(Uri left, Uri right)
    {
        return string.Equals(left.AbsoluteUri.TrimEnd('/'), right.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
