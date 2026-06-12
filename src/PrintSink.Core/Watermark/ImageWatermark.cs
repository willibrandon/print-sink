namespace PrintSink.Core.Watermark;

/// <summary>
/// Describes an image watermark to apply to XPS content before conversion.
/// </summary>
public sealed class ImageWatermark
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageWatermark"/> class.
    /// </summary>
    /// <param name="imagePath">The image file path.</param>
    /// <param name="width">The image width in DIPs.</param>
    /// <param name="height">The image height in DIPs.</param>
    /// <param name="opacity">The opacity from 0.0 through 1.0.</param>
    /// <param name="rotationDegrees">The clockwise rotation in degrees.</param>
    /// <param name="offsetX">The horizontal offset in DIPs.</param>
    /// <param name="offsetY">The vertical offset in DIPs.</param>
    public ImageWatermark(
        string imagePath,
        double width,
        double height,
        double opacity,
        double rotationDegrees,
        double offsetX,
        double offsetY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        ValidateOpacity(opacity);

        ImagePath = imagePath;
        Width = width;
        Height = height;
        Opacity = opacity;
        RotationDegrees = rotationDegrees;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// Gets the image file path.
    /// </summary>
    public string ImagePath { get; }

    /// <summary>
    /// Gets the image width in DIPs.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the image height in DIPs.
    /// </summary>
    public double Height { get; }

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
