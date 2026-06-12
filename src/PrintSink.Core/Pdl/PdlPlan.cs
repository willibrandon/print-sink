namespace PrintSink.Core.Pdl;

/// <summary>
/// Describes the selected routing action for a PDL stream.
/// </summary>
public sealed class PdlPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdlPlan"/> class.
    /// </summary>
    /// <param name="actionKind">The routing action to perform.</param>
    /// <param name="sourceFormat">The parsed source format, or <see langword="null"/> when unknown.</param>
    /// <param name="targetFormat">The endpoint target format.</param>
    /// <param name="conversionKind">The conversion to perform when <paramref name="actionKind"/> is <see cref="PdlActionKind.Convert"/>.</param>
    /// <param name="reason">A short diagnostic reason for the decision.</param>
    public PdlPlan(
        PdlActionKind actionKind,
        PdlFormat? sourceFormat,
        PdlFormat targetFormat,
        PdlConversionKind? conversionKind,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        ActionKind = actionKind;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        ConversionKind = conversionKind;
        Reason = reason;
    }

    /// <summary>
    /// Gets the routing action to perform.
    /// </summary>
    public PdlActionKind ActionKind { get; }

    /// <summary>
    /// Gets the parsed source format, or <see langword="null"/> when unknown.
    /// </summary>
    public PdlFormat? SourceFormat { get; }

    /// <summary>
    /// Gets the endpoint target format.
    /// </summary>
    public PdlFormat TargetFormat { get; }

    /// <summary>
    /// Gets the conversion to perform, when conversion is required.
    /// </summary>
    public PdlConversionKind? ConversionKind { get; }

    /// <summary>
    /// Gets a short diagnostic reason for the decision.
    /// </summary>
    public string Reason { get; }
}
