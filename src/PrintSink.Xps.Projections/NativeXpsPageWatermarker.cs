using PrintSink.Core.Watermark;

namespace PrintSink.Xps.Projections;

/// <summary>
/// Wraps the generated native XPS page watermarker projection.
/// </summary>
public sealed class NativeXpsPageWatermarker
{
    private readonly PrintSink.Xps.XpsPageWatermarker watermarker = new();

    /// <summary>
    /// Applies text watermark options to the native watermarker.
    /// </summary>
    /// <param name="text">The text watermark options.</param>
    public void ApplyText(TextWatermark text)
    {
        ArgumentNullException.ThrowIfNull(text);

        watermarker.Text = text.Text;
        watermarker.FontFamily = text.FontFamily;
        watermarker.FontSize = text.FontSize;
        watermarker.Opacity = text.Opacity;
        watermarker.RotationDegrees = text.RotationDegrees;
        watermarker.OffsetX = text.OffsetX;
        watermarker.OffsetY = text.OffsetY;
    }
}
