namespace PrintSink.Core.Capabilities;

/// <summary>
/// Describes a Print Device Capabilities feature and the options PrintSink adds to it.
/// </summary>
public sealed class CustomFeature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFeature"/> class.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <param name="options">The options to add to the feature.</param>
    public CustomFeature(PrintSchemaQualifiedName name, IEnumerable<CustomFeatureOption> options)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);

        Name = name;
        Options = [.. options];

        if (Options.Count == 0)
        {
            throw new ArgumentException("At least one option is required.", nameof(options));
        }
    }

    /// <summary>
    /// Gets the feature name.
    /// </summary>
    public PrintSchemaQualifiedName Name { get; }

    /// <summary>
    /// Gets the options to add to the feature.
    /// </summary>
    public IReadOnlyList<CustomFeatureOption> Options { get; }
}
