using System.Collections.ObjectModel;

namespace PrintSink.Capabilities;

/// <summary>
/// Describes one option under a custom Print Device Capabilities feature.
/// </summary>
public sealed class CustomFeatureOption
{
    private readonly Dictionary<string, string> scoredProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFeatureOption"/> class.
    /// </summary>
    /// <param name="name">The option token without a namespace prefix.</param>
    /// <param name="displayName">The localized or fallback display name.</param>
    /// <param name="scoredProperties">Additional scored properties to add to the option.</param>
    public CustomFeatureOption(string name, string displayName, IReadOnlyDictionary<string, string>? scoredProperties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Name = name;
        DisplayName = displayName;
        this.scoredProperties = scoredProperties is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(scoredProperties, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the option token without a namespace prefix.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the localized or fallback display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets additional scored properties to add to the option.
    /// </summary>
    public IReadOnlyDictionary<string, string> ScoredProperties => new ReadOnlyDictionary<string, string>(scoredProperties);
}
