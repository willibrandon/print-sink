using PrintSink.Core.Endpoints;

namespace PrintSink.Cli;

internal static class EndpointParser
{
    public static bool TryParse(string text, out EndpointKind endpointKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        string normalized = text.Trim().Replace("_", "-", StringComparison.Ordinal).ToUpperInvariant();
        endpointKind = normalized switch
        {
            "PDF" => EndpointKind.Pdf,
            "XPS" => EndpointKind.Xps,
            "POSTSCRIPT" or "POST-SCRIPT" or "PS" => EndpointKind.PostScript,
            "CLOUD" => EndpointKind.Cloud,
            "PWG" or "PWG-RASTER" or "PWGRASTER" => EndpointKind.PwgRaster,
            _ => default,
        };

        return endpointKind != default || string.Equals(normalized, "PDF", StringComparison.Ordinal);
    }
}
