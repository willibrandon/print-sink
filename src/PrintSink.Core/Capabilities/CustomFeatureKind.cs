namespace PrintSink.Capabilities;

/// <summary>
/// Identifies the category of a custom PrintSink print capability feature.
/// </summary>
public enum CustomFeatureKind
{
    /// <summary>
    /// A custom media size feature.
    /// </summary>
    MediaSize,

    /// <summary>
    /// A custom media type feature.
    /// </summary>
    MediaType,

    /// <summary>
    /// A custom print resolution feature.
    /// </summary>
    Resolution,

    /// <summary>
    /// A custom input bin feature.
    /// </summary>
    InputBin,

    /// <summary>
    /// A custom output bin feature.
    /// </summary>
    OutputBin,

    /// <summary>
    /// A custom staple feature.
    /// </summary>
    Staple,

    /// <summary>
    /// A custom page-order feature.
    /// </summary>
    PageOrder,

    /// <summary>
    /// A custom watermark feature.
    /// </summary>
    Watermark,
}
