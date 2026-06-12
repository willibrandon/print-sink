namespace PrintSink.Core.Watermark;

/// <summary>
/// Describes a text watermark to apply to XPS content before conversion.
/// </summary>
public sealed class TextWatermark
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextWatermark"/> class.
    /// </summary>
    /// <param name="text">The watermark text.</param>
    /// <param name="fontFamily">The font family name.</param>
    /// <param name="fontSize">The font size in DIPs.</param>
    /// <param name="opacity">The opacity from 0.0 through 1.0.</param>
    /// <param name="rotationDegrees">The clockwise rotation in degrees.</param>
    /// <param name="offsetX">The horizontal offset in DIPs.</param>
    /// <param name="offsetY">The vertical offset in DIPs.</param>
    public TextWatermark(
        string text,
        string fontFamily,
        double fontSize,
        double opacity,
        double rotationDegrees,
        double offsetX,
        double offsetY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);

        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "Font size must be greater than zero.");
        }

        ValidateOpacity(opacity);

        Text = text;
        FontFamily = fontFamily;
        FontSize = fontSize;
        Opacity = opacity;
        RotationDegrees = rotationDegrees;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// Gets the watermark text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the font family name.
    /// </summary>
    public string FontFamily { get; }

    /// <summary>
    /// Gets the font size in DIPs.
    /// </summary>
    public double FontSize { get; }

    /// <summary>
    /// Gets the opacity from 0.0 through 1.0.
    /// </summary>
    public double Opacity { get; }

    /// <summary>
    /// Gets the clockwise rotation in degrees.
    /// </summary>
    public double RotationDegrees { get; }

    /// <summary>
    /// Gets the horizontal offset in DIPs.
    /// </summary>
    public double OffsetX { get; }

    /// <summary>
    /// Gets the vertical offset in DIPs.
    /// </summary>
    public double OffsetY { get; }

    private static void ValidateOpacity(double opacity)
    {
        if (opacity is < 0 or > 1 || double.IsNaN(opacity))
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), opacity, "Opacity must be between zero and one.");
        }
    }
}
