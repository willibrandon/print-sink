namespace PrintSink.Core.Capabilities;

/// <summary>
/// Describes an option to add to a Print Device Capabilities feature.
/// </summary>
public sealed class CustomFeatureOption
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFeatureOption"/> class.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <param name="isDefault">A value indicating whether this option becomes the feature default.</param>
    /// <param name="properties">Properties to place under the option.</param>
    public CustomFeatureOption(
        PrintSchemaQualifiedName name,
        bool isDefault,
        IEnumerable<PrintSchemaProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(properties);

        Name = name;
        IsDefault = isDefault;
        Properties = [.. properties];
    }

    /// <summary>
    /// Gets the option name.
    /// </summary>
    public PrintSchemaQualifiedName Name { get; }

    /// <summary>
    /// Gets a value indicating whether this option becomes the feature default.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Gets properties to place under the option.
    /// </summary>
    public IReadOnlyList<PrintSchemaProperty> Properties { get; }
}
