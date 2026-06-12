namespace PrintSink.Core.Capabilities;

/// <summary>
/// Defines the Print Schema node kind for a property carried by an option.
/// </summary>
public enum PrintSchemaPropertyKind
{
    /// <summary>
    /// A regular Print Schema property.
    /// </summary>
    Property,

    /// <summary>
    /// A scored property used when comparing option quality.
    /// </summary>
    ScoredProperty,
}
