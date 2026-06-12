namespace PrintSink.Watermark;

/// <summary>
/// Describes a text watermark to place on XPS pages.
/// </summary>
public sealed class TextWatermark
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextWatermark"/> class.
    /// </summary>
    /// <param name="text">The watermark text.</param>
    /// <param name="fontSizePoints">The font size in points.</param>
    /// <param name="opacity">The opacity from 0.0 to 1.0.</param>
    /// <param name="rotationDegrees">The clockwise rotation in degrees.</param>
    /// <param name="xOffsetDips">The horizontal offset in device-independent pixels.</param>
    /// <param name="yOffsetDips">The vertical offset in device-independent pixels.</param>
    public TextWatermark(string text, double fontSizePoints, double opacity, double rotationDegrees, double xOffsetDips, double yOffsetDips)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSizePoints);
        ArgumentOutOfRangeException.ThrowIfNegative(opacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1.0);

        Text = text;
        FontSizePoints = fontSizePoints;
        Opacity = opacity;
        RotationDegrees = rotationDegrees;
        XOffsetDips = xOffsetDips;
        YOffsetDips = yOffsetDips;
    }

    /// <summary>
    /// Gets the watermark text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the font size in points.
    /// </summary>
    public double FontSizePoints { get; }

    /// <summary>
    /// Gets the opacity from 0.0 to 1.0.
    /// </summary>
    public double Opacity { get; }

    /// <summary>
    /// Gets the clockwise rotation in degrees.
    /// </summary>
    public double RotationDegrees { get; }

    /// <summary>
    /// Gets the horizontal offset in device-independent pixels.
    /// </summary>
    public double XOffsetDips { get; }

    /// <summary>
    /// Gets the vertical offset in device-independent pixels.
    /// </summary>
    public double YOffsetDips { get; }
}
