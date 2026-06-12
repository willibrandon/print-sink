using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintSink.Watermark;

/// <summary>
/// Describes effective watermark choices for a print job.
/// </summary>
public sealed class WatermarkOptions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WatermarkOptions"/> class.
    /// </summary>
    /// <param name="isTextEnabled">Whether text watermarking is enabled.</param>
    /// <param name="text">The text watermark settings.</param>
    /// <param name="isImageEnabled">Whether image watermarking is enabled.</param>
    /// <param name="image">The image watermark settings.</param>
    public WatermarkOptions(bool isTextEnabled, TextWatermark? text, bool isImageEnabled, ImageWatermark? image)
    {
        if (isTextEnabled && text is null)
        {
            throw new ArgumentException("A text watermark must be supplied when text watermarking is enabled.", nameof(text));
        }

        if (isImageEnabled && image is null)
        {
            throw new ArgumentException("An image watermark must be supplied when image watermarking is enabled.", nameof(image));
        }

        IsTextEnabled = isTextEnabled;
        Text = text;
        IsImageEnabled = isImageEnabled;
        Image = image;
    }

    /// <summary>
    /// Gets disabled watermark options.
    /// </summary>
    public static WatermarkOptions Disabled { get; } = new(false, null, false, null);

    /// <summary>
    /// Gets a value indicating whether text watermarking is enabled.
    /// </summary>
    public bool IsTextEnabled { get; }

    /// <summary>
    /// Gets the text watermark settings.
    /// </summary>
    public TextWatermark? Text { get; }

    /// <summary>
    /// Gets a value indicating whether image watermarking is enabled.
    /// </summary>
    public bool IsImageEnabled { get; }

    /// <summary>
    /// Gets the image watermark settings.
    /// </summary>
    public ImageWatermark? Image { get; }

    /// <summary>
    /// Gets a value indicating whether any watermark operation is enabled.
    /// </summary>
    public bool IsEnabled => IsTextEnabled || IsImageEnabled;

    /// <summary>
    /// Serializes the options to JSON for settings storage.
    /// </summary>
    /// <returns>A compact JSON payload.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    /// <summary>
    /// Deserializes options from JSON.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The deserialized options.</returns>
    /// <exception cref="JsonException">Thrown when the payload is invalid.</exception>
    public static WatermarkOptions FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        WatermarkOptions? options = JsonSerializer.Deserialize<WatermarkOptions>(json, SerializerOptions);
        return options ?? throw new JsonException("Watermark options JSON did not produce a value.");
    }
}
