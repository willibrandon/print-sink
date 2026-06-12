namespace PrintSink.Core.Pdl;

/// <summary>
/// Selects the best printer document format for a workflow job.
/// </summary>
public static class PrinterDocumentFormatSelector
{
    /// <summary>
    /// Selects the target document format from printer default and supported IPP attributes.
    /// </summary>
    /// <param name="sourceContentType">The source PDL content type.</param>
    /// <param name="defaultDocumentFormat">The printer's document-format-default value.</param>
    /// <param name="supportedDocumentFormats">The printer's document-format-supported values.</param>
    /// <returns>The selected submission plan.</returns>
    public static PrinterDocumentFormatPlan Select(
        string sourceContentType,
        string? defaultDocumentFormat,
        IEnumerable<string> supportedDocumentFormats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);
        ArgumentNullException.ThrowIfNull(supportedDocumentFormats);

        if (!PdlFormatInfo.TryParseContentType(sourceContentType, out PdlFormat sourceFormat))
        {
            return new PrinterDocumentFormatPlan(sourceContentType, sourceContentType, null, null, null);
        }

        foreach (string candidate in GetCandidates(defaultDocumentFormat, supportedDocumentFormats))
        {
            if (TryCreatePlan(sourceContentType, sourceFormat, candidate, out PrinterDocumentFormatPlan? plan)
                && plan is not null)
            {
                return plan;
            }
        }

        return new PrinterDocumentFormatPlan(sourceContentType, sourceContentType, sourceFormat, sourceFormat, null);
    }

    private static IEnumerable<string> GetCandidates(
        string? defaultDocumentFormat,
        IEnumerable<string> supportedDocumentFormats)
    {
        if (!string.IsNullOrWhiteSpace(defaultDocumentFormat))
        {
            yield return defaultDocumentFormat;
        }

        foreach (string supportedFormat in supportedDocumentFormats)
        {
            if (!string.IsNullOrWhiteSpace(supportedFormat)
                && !string.Equals(defaultDocumentFormat, supportedFormat, StringComparison.OrdinalIgnoreCase))
            {
                yield return supportedFormat;
            }
        }
    }

    private static bool TryCreatePlan(
        string sourceContentType,
        PdlFormat sourceFormat,
        string candidateContentType,
        out PrinterDocumentFormatPlan? plan)
    {
        plan = null;
        if (!PdlFormatInfo.TryParseContentType(candidateContentType, out PdlFormat targetFormat))
        {
            return false;
        }

        if (CanCopy(sourceFormat, targetFormat))
        {
            plan = new PrinterDocumentFormatPlan(
                sourceContentType,
                candidateContentType,
                sourceFormat,
                targetFormat,
                null);
            return true;
        }

        if (TryGetConversion(sourceFormat, targetFormat, out PdlConversionKind conversionKind))
        {
            plan = new PrinterDocumentFormatPlan(
                sourceContentType,
                candidateContentType,
                sourceFormat,
                targetFormat,
                conversionKind);
            return true;
        }

        return false;
    }

    private static bool CanCopy(PdlFormat sourceFormat, PdlFormat targetFormat)
    {
        return sourceFormat == targetFormat
            || (sourceFormat is PdlFormat.Oxps or PdlFormat.Xps
                && targetFormat is PdlFormat.Oxps or PdlFormat.Xps);
    }

    private static bool TryGetConversion(
        PdlFormat sourceFormat,
        PdlFormat targetFormat,
        out PdlConversionKind conversionKind)
    {
        if (sourceFormat is PdlFormat.Oxps or PdlFormat.Xps)
        {
            switch (targetFormat)
            {
                case PdlFormat.Pdf:
                    conversionKind = PdlConversionKind.XpsToPdf;
                    return true;
                case PdlFormat.PwgRaster:
                    conversionKind = PdlConversionKind.XpsToPwgRaster;
                    return true;
                case PdlFormat.Pclm:
                    conversionKind = PdlConversionKind.XpsToPclm;
                    return true;
            }
        }

        conversionKind = default;
        return false;
    }
}
