namespace PrintSink.Core.Pdl;

/// <summary>
/// Parses and formats PDL content types.
/// </summary>
public static class PdlFormatInfo
{
    /// <summary>
    /// Gets the OXPS content type.
    /// </summary>
    public const string OxpsContentType = "application/oxps";

    /// <summary>
    /// Gets the XPS content type.
    /// </summary>
    public const string XpsContentType = "application/vnd.ms-xpsdocument";

    /// <summary>
    /// Gets the PDF content type.
    /// </summary>
    public const string PdfContentType = "application/pdf";

    /// <summary>
    /// Gets the PostScript content type.
    /// </summary>
    public const string PostScriptContentType = "application/postscript";

    /// <summary>
    /// Gets the PWG Raster content type.
    /// </summary>
    public const string PwgRasterContentType = "image/pwg-raster";

    /// <summary>
    /// Gets the PCLm content type.
    /// </summary>
    public const string PclmContentType = "application/pclm";

    /// <summary>
    /// Tries to parse a content type into a PDL format.
    /// </summary>
    /// <param name="contentType">The content type, with or without parameters.</param>
    /// <param name="format">The parsed format.</param>
    /// <returns><see langword="true"/> when the content type is recognized; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseContentType(string contentType, out PdlFormat format)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        string normalized = Normalize(contentType);

        format = normalized switch
        {
            OxpsContentType => PdlFormat.Oxps,
            XpsContentType => PdlFormat.Xps,
            PdfContentType => PdlFormat.Pdf,
            PostScriptContentType or "application/ps" => PdlFormat.PostScript,
            PwgRasterContentType or "application/pwg-raster" => PdlFormat.PwgRaster,
            PclmContentType or "application/vnd.hp-pclm" => PdlFormat.Pclm,
            _ => default,
        };

        return normalized is
            OxpsContentType or
            XpsContentType or
            PdfContentType or
            PostScriptContentType or
            "application/ps" or
            PwgRasterContentType or
            "application/pwg-raster" or
            PclmContentType or
            "application/vnd.hp-pclm";
    }

    /// <summary>
    /// Gets the canonical content type for a PDL format.
    /// </summary>
    /// <param name="format">The PDL format.</param>
    /// <returns>The canonical content type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not recognized.</exception>
    public static string GetContentType(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Oxps => OxpsContentType,
            PdlFormat.Xps => XpsContentType,
            PdlFormat.Pdf => PdfContentType,
            PdlFormat.PostScript => PostScriptContentType,
            PdlFormat.PwgRaster => PwgRasterContentType,
            PdlFormat.Pclm => PclmContentType,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported PDL format."),
        };
    }

    /// <summary>
    /// Gets the maximum supported version advertised for a pass-through PDL format.
    /// </summary>
    /// <param name="format">The PDL format.</param>
    /// <returns>The maximum supported major/minor version string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not recognized.</exception>
    public static string GetMaxSupportedVersion(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Pdf => "1.7",
            PdlFormat.PostScript => "3.0",
            PdlFormat.Oxps or PdlFormat.Xps => "1.0",
            PdlFormat.PwgRaster or PdlFormat.Pclm => "1.0",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported PDL format."),
        };
    }

    private static string Normalize(string contentType)
    {
        int parameterStart = contentType.IndexOf(';', StringComparison.Ordinal);
        string mediaType = parameterStart >= 0 ? contentType[..parameterStart] : contentType;

        return mediaType.Trim().ToLowerInvariant();
    }
}
