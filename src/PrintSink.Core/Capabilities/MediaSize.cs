using System.Globalization;

namespace PrintSink.Capabilities;

/// <summary>
/// Describes a custom media size in microns.
/// </summary>
public sealed class MediaSize
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSize"/> class.
    /// </summary>
    /// <param name="name">The media size token without a namespace prefix.</param>
    /// <param name="displayName">The localized or fallback display name.</param>
    /// <param name="widthMicrons">The media width in microns.</param>
    /// <param name="heightMicrons">The media height in microns.</param>
    public MediaSize(string name, string displayName, int widthMicrons, int heightMicrons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthMicrons);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightMicrons);

        Name = name;
        DisplayName = displayName;
        WidthMicrons = widthMicrons;
        HeightMicrons = heightMicrons;
    }

    /// <summary>
    /// Gets the media size token without a namespace prefix.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the localized or fallback display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the media width in microns.
    /// </summary>
    public int WidthMicrons { get; }

    /// <summary>
    /// Gets the media height in microns.
    /// </summary>
    public int HeightMicrons { get; }

    /// <summary>
    /// Converts the media size to a custom feature option.
    /// </summary>
    /// <returns>A custom feature option.</returns>
    public CustomFeatureOption ToFeatureOption()
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["MediaSizeWidth"] = WidthMicrons.ToString(CultureInfo.InvariantCulture),
            ["MediaSizeHeight"] = HeightMicrons.ToString(CultureInfo.InvariantCulture),
        };

        return new CustomFeatureOption(Name, DisplayName, properties);
    }
}
