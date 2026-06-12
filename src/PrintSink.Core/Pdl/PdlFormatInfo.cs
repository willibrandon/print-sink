using System.Diagnostics.CodeAnalysis;

namespace PrintSink.Pdl;

/// <summary>
/// Provides canonical content types and helpers for page description languages.
/// </summary>
public static class PdlFormatInfo
{
    /// <summary>
    /// Canonical OXPS content type.
    /// </summary>
    public const string OxpsContentType = "application/oxps";

    /// <summary>
    /// Canonical PDF content type.
    /// </summary>
    public const string PdfContentType = "application/pdf";

    /// <summary>
    /// Canonical PostScript content type.
    /// </summary>
    public const string PostScriptContentType = "application/postscript";

    /// <summary>
    /// Canonical XPS content type.
    /// </summary>
    public const string XpsContentType = "application/vnd.ms-xpsdocument";

    /// <summary>
    /// Canonical PWG Raster content type.
    /// </summary>
    public const string PwgRasterContentType = "image/pwg-raster";

    /// <summary>
    /// Canonical PCLm content type.
    /// </summary>
    public const string PclmContentType = "application/PCLm";

    /// <summary>
    /// Converts a MIME content type to a <see cref="PdlFormat"/>.
    /// </summary>
    /// <param name="contentType">The MIME content type to parse.</param>
    /// <returns>The matching PDL format, or <see cref="PdlFormat.Unknown"/>.</returns>
    public static PdlFormat FromContentType(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        string normalized = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        return normalized.ToUpperInvariant() switch
        {
            "APPLICATION/OXPS" => PdlFormat.Oxps,
            "APPLICATION/PDF" => PdlFormat.Pdf,
            "APPLICATION/POSTSCRIPT" => PdlFormat.PostScript,
            "APPLICATION/VND.MS-XPSDOCUMENT" => PdlFormat.Xps,
            "IMAGE/PWG-RASTER" => PdlFormat.PwgRaster,
            "APPLICATION/PCLM" => PdlFormat.Pclm,
            _ => PdlFormat.Unknown,
        };
    }

    /// <summary>
    /// Converts a known PDL format to its canonical MIME content type.
    /// </summary>
    /// <param name="format">The PDL format to convert.</param>
    /// <returns>The canonical MIME content type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the format is unknown.</exception>
    public static string ToContentType(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Oxps => OxpsContentType,
            PdlFormat.Pdf => PdfContentType,
            PdlFormat.PostScript => PostScriptContentType,
            PdlFormat.Xps => XpsContentType,
            PdlFormat.PwgRaster => PwgRasterContentType,
            PdlFormat.Pclm => PclmContentType,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown PDL format has no content type."),
        };
    }

    /// <summary>
    /// Converts a known PDL format to its default file extension.
    /// </summary>
    /// <param name="format">The PDL format to convert.</param>
    /// <returns>The extension including the leading period.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the format is unknown.</exception>
    public static string ToExtension(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Oxps => ".oxps",
            PdlFormat.Pdf => ".pdf",
            PdlFormat.PostScript => ".ps",
            PdlFormat.Xps => ".xps",
            PdlFormat.PwgRaster => ".pwg",
            PdlFormat.Pclm => ".pclm",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown PDL format has no extension."),
        };
    }

    /// <summary>
    /// Returns whether the format can be modified by the native XPS watermarking component.
    /// </summary>
    /// <param name="format">The PDL format to inspect.</param>
    /// <returns><see langword="true"/> when XPS object model watermarking is possible.</returns>
    public static bool IsXpsFamily(PdlFormat format)
    {
        return format is PdlFormat.Oxps or PdlFormat.Xps;
    }

    /// <summary>
    /// Attempts to parse a MIME content type without throwing.
    /// </summary>
    /// <param name="contentType">The content type to parse.</param>
    /// <param name="format">The parsed format.</param>
    /// <returns><see langword="true"/> when the content type maps to a known format.</returns>
    public static bool TryFromContentType([NotNullWhen(true)] string? contentType, out PdlFormat format)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            format = PdlFormat.Unknown;
            return false;
        }

        format = FromContentType(contentType);
        return format != PdlFormat.Unknown;
    }
}
