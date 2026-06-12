namespace PrintSink.Pdl;

/// <summary>
/// Describes how a source PDL stream should be handled.
/// </summary>
public sealed class PdlPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdlPlan" /> class.
    /// </summary>
    /// <param name="action">The selected action.</param>
    /// <param name="sourceFormat">The detected source format, if known.</param>
    /// <param name="targetFormat">The endpoint target format.</param>
    /// <param name="conversionKind">The conversion to run, if any.</param>
    /// <param name="reason">A short reason for the selected plan.</param>
    public PdlPlan(
        PdlActionKind action,
        PdlFormat? sourceFormat,
        PdlFormat targetFormat,
        PdlConversionKind? conversionKind,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Action = action;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        ConversionKind = conversionKind;
        Reason = reason;
    }

    /// <summary>
    /// Gets the selected action.
    /// </summary>
    public PdlActionKind Action { get; }

    /// <summary>
    /// Gets the detected source format, if known.
    /// </summary>
    public PdlFormat? SourceFormat { get; }

    /// <summary>
    /// Gets the endpoint target format.
    /// </summary>
    public PdlFormat TargetFormat { get; }

    /// <summary>
    /// Gets the conversion to run, if any.
    /// </summary>
    public PdlConversionKind? ConversionKind { get; }

    /// <summary>
    /// Gets a short reason for the selected plan.
    /// </summary>
    public string Reason { get; }
}
