namespace PrintSink.Pdl;

/// <summary>
/// Represents the immutable processing plan for one virtual printer payload.
/// </summary>
public sealed class PdlPlan
{
    private PdlPlan(
        PdlActionKind action,
        PdlFormat sourceFormat,
        PdlFormat targetFormat,
        PdlConversionKind conversion,
        bool requiresWatermark,
        string? rejectionReason)
    {
        Action = action;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        Conversion = conversion;
        RequiresWatermark = requiresWatermark;
        RejectionReason = rejectionReason;
    }

    /// <summary>
    /// Gets the action to perform.
    /// </summary>
    public PdlActionKind Action { get; }

    /// <summary>
    /// Gets the source PDL format.
    /// </summary>
    public PdlFormat SourceFormat { get; }

    /// <summary>
    /// Gets the target PDL format expected by the endpoint sink.
    /// </summary>
    public PdlFormat TargetFormat { get; }

    /// <summary>
    /// Gets the Windows print workflow conversion required for the plan.
    /// </summary>
    public PdlConversionKind Conversion { get; }

    /// <summary>
    /// Gets the target MIME content type.
    /// </summary>
    public string TargetContentType => PdlFormatInfo.ToContentType(TargetFormat);

    /// <summary>
    /// Gets a value indicating whether the native XPS watermarking component must run before copy or conversion.
    /// </summary>
    public bool RequiresWatermark { get; }

    /// <summary>
    /// Gets the rejection reason when <see cref="Action"/> is <see cref="PdlActionKind.Reject"/>.
    /// </summary>
    public string? RejectionReason { get; }

    /// <summary>
    /// Creates a direct-copy plan.
    /// </summary>
    /// <param name="sourceFormat">The source format to copy.</param>
    /// <param name="targetFormat">The target format produced by the copy.</param>
    /// <param name="requiresWatermark">Whether XPS watermarking must run before the copy.</param>
    /// <returns>A copy plan.</returns>
    public static PdlPlan Copy(PdlFormat sourceFormat, PdlFormat targetFormat, bool requiresWatermark = false)
    {
        return new PdlPlan(PdlActionKind.Copy, sourceFormat, targetFormat, PdlConversionKind.None, requiresWatermark, null);
    }

    /// <summary>
    /// Creates a conversion plan.
    /// </summary>
    /// <param name="sourceFormat">The source format to convert.</param>
    /// <param name="targetFormat">The target format produced by the converter.</param>
    /// <param name="conversion">The converter operation to request from the print workflow.</param>
    /// <param name="requiresWatermark">Whether XPS watermarking must run before conversion.</param>
    /// <returns>A conversion plan.</returns>
    public static PdlPlan Convert(PdlFormat sourceFormat, PdlFormat targetFormat, PdlConversionKind conversion, bool requiresWatermark)
    {
        return new PdlPlan(PdlActionKind.Convert, sourceFormat, targetFormat, conversion, requiresWatermark, null);
    }

    /// <summary>
    /// Creates a rejection plan.
    /// </summary>
    /// <param name="sourceFormat">The rejected source format.</param>
    /// <param name="targetFormat">The target endpoint format.</param>
    /// <param name="reason">The rejection reason.</param>
    /// <returns>A rejection plan.</returns>
    public static PdlPlan Reject(PdlFormat sourceFormat, PdlFormat targetFormat, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new PdlPlan(PdlActionKind.Reject, sourceFormat, targetFormat, PdlConversionKind.None, false, reason);
    }
}
