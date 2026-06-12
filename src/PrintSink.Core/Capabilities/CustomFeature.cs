namespace PrintSink.Capabilities;

/// <summary>
/// Describes one custom feature to inject into Print Device Capabilities XML.
/// </summary>
public sealed class CustomFeature
{
    private readonly CustomFeatureOption[] options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFeature"/> class.
    /// </summary>
    /// <param name="kind">The feature kind.</param>
    /// <param name="name">The feature token without a namespace prefix.</param>
    /// <param name="displayName">The localized or fallback display name.</param>
    /// <param name="selectionMode">The feature selection mode.</param>
    /// <param name="options">The feature options.</param>
    public CustomFeature(
        CustomFeatureKind kind,
        string name,
        string displayName,
        CustomFeatureSelectionMode selectionMode,
        IEnumerable<CustomFeatureOption> options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.ToArray();
        ArgumentOutOfRangeException.ThrowIfZero(this.options.Length);

        Kind = kind;
        Name = name;
        DisplayName = displayName;
        SelectionMode = selectionMode;
    }

    /// <summary>
    /// Gets the feature kind.
    /// </summary>
    public CustomFeatureKind Kind { get; }

    /// <summary>
    /// Gets the feature token without a namespace prefix.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the localized or fallback display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the feature selection mode.
    /// </summary>
    public CustomFeatureSelectionMode SelectionMode { get; }

    /// <summary>
    /// Gets the feature options.
    /// </summary>
    public IReadOnlyList<CustomFeatureOption> Options => options;
}
