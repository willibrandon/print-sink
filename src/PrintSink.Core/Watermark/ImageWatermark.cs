namespace PrintSink.Watermark;

/// <summary>
/// Describes an image watermark to place on XPS pages.
/// </summary>
public sealed class ImageWatermark
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageWatermark"/> class.
    /// </summary>
    /// <param name="path">The package or local path to the watermark image.</param>
    /// <param name="dpiX">The horizontal image DPI.</param>
    /// <param name="dpiY">The vertical image DPI.</param>
    /// <param name="widthDips">The rendered width in device-independent pixels.</param>
    /// <param name="heightDips">The rendered height in device-independent pixels.</param>
    /// <param name="opacity">The opacity from 0.0 to 1.0.</param>
    public ImageWatermark(string path, double dpiX, double dpiY, double widthDips, double heightDips, double opacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthDips);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightDips);
        ArgumentOutOfRangeException.ThrowIfNegative(opacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opacity, 1.0);

        Path = path;
        DpiX = dpiX;
        DpiY = dpiY;
        WidthDips = widthDips;
        HeightDips = heightDips;
        Opacity = opacity;
    }

    /// <summary>
    /// Gets the package or local path to the watermark image.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the horizontal image DPI.
    /// </summary>
    public double DpiX { get; }

    /// <summary>
    /// Gets the vertical image DPI.
    /// </summary>
    public double DpiY { get; }

    /// <summary>
    /// Gets the rendered width in device-independent pixels.
    /// </summary>
    public double WidthDips { get; }

    /// <summary>
    /// Gets the rendered height in device-independent pixels.
    /// </summary>
    public double HeightDips { get; }

    /// <summary>
    /// Gets the opacity from 0.0 to 1.0.
    /// </summary>
    public double Opacity { get; }
}
