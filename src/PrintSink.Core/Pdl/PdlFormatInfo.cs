namespace PrintSink.Pdl;

/// <summary>
/// Maps PDL formats to content types.
/// </summary>
public static class PdlFormatInfo
{
    /// <summary>
    /// Gets the content type for a format.
    /// </summary>
    /// <param name="format">The PDL format.</param>
    /// <returns>The content type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format" /> is unknown.</exception>
    public static string GetContentType(PdlFormat format) =>
        format switch
        {
            PdlFormat.Oxps => "application/oxps",
            PdlFormat.Xps => "application/vnd.ms-xpsdocument",
            PdlFormat.Pdf => "application/pdf",
            PdlFormat.PostScript => "application/postscript",
            PdlFormat.PwgRaster => "image/pwg-raster",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown PDL format."),
        };

    /// <summary>
    /// Attempts to parse a content type.
    /// </summary>
    /// <param name="contentType">The content type to parse.</param>
    /// <param name="format">The parsed format when successful.</param>
    /// <returns><see langword="true" /> when the content type is known.</returns>
    public static bool TryParseContentType(string contentType, out PdlFormat format)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        foreach (PdlFormat candidate in Enum.GetValues<PdlFormat>())
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(contentType, GetContentType(candidate)))
            {
                format = candidate;
                return true;
            }
        }

        format = default;
        return false;
    }
}
