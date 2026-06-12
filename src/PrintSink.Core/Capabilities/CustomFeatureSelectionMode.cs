namespace PrintSink.Capabilities;

/// <summary>
/// Describes whether a custom capability feature allows one or multiple selected options.
/// </summary>
public enum CustomFeatureSelectionMode
{
    /// <summary>
    /// Exactly one option can be selected.
    /// </summary>
    PickOne,

    /// <summary>
    /// Multiple options can be selected.
    /// </summary>
    PickMany,
}
