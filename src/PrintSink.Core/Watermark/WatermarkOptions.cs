namespace PrintSink.Core.Watermark;

/// <summary>
/// Describes watermark settings captured by the UI and consumed by job processing.
/// </summary>
public sealed class WatermarkOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WatermarkOptions"/> class.
    /// </summary>
    /// <param name="enabled">A value indicating whether watermarking is enabled.</param>
    /// <param name="text">The text watermark, when configured.</param>
    /// <param name="image">The image watermark, when configured.</param>
    public WatermarkOptions(bool enabled, TextWatermark? text, ImageWatermark? image)
    {
        if (enabled && text is null && image is null)
        {
            throw new ArgumentException("Enabled watermark options require text or image settings.", nameof(enabled));
        }

        Enabled = enabled;
        Text = text;
        Image = image;
    }

    /// <summary>
    /// Gets a disabled watermark options instance.
    /// </summary>
    public static WatermarkOptions Disabled { get; } = new(false, null, null);

    /// <summary>
    /// Gets a value indicating whether watermarking is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the text watermark, when configured.
    /// </summary>
    public TextWatermark? Text { get; }

    /// <summary>
    /// Gets the image watermark, when configured.
    /// </summary>
    public ImageWatermark? Image { get; }
}
