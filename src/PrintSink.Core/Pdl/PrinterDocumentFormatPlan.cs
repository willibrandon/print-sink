namespace PrintSink.Core.Pdl;

/// <summary>
/// Describes how a workflow job should be submitted to a physical printer.
/// </summary>
public sealed class PrinterDocumentFormatPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrinterDocumentFormatPlan"/> class.
    /// </summary>
    /// <param name="sourceContentType">The source PDL content type.</param>
    /// <param name="targetContentType">The target printer document format.</param>
    /// <param name="sourceFormat">The parsed source format, if recognized.</param>
    /// <param name="targetFormat">The parsed target format, if recognized.</param>
    /// <param name="conversionKind">The conversion to run before submission, if required.</param>
    public PrinterDocumentFormatPlan(
        string sourceContentType,
        string targetContentType,
        PdlFormat? sourceFormat,
        PdlFormat? targetFormat,
        PdlConversionKind? conversionKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetContentType);

        SourceContentType = sourceContentType;
        TargetContentType = targetContentType;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        ConversionKind = conversionKind;
    }

    /// <summary>
    /// Gets the source PDL content type.
    /// </summary>
    public string SourceContentType { get; }

    /// <summary>
    /// Gets the target printer document format.
    /// </summary>
    public string TargetContentType { get; }

    /// <summary>
    /// Gets the parsed source format, if recognized.
    /// </summary>
    public PdlFormat? SourceFormat { get; }

    /// <summary>
    /// Gets the parsed target format, if recognized.
    /// </summary>
    public PdlFormat? TargetFormat { get; }

    /// <summary>
    /// Gets the conversion to run before submission, if required.
    /// </summary>
    public PdlConversionKind? ConversionKind { get; }
}
