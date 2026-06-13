using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Watermark;

/// <summary>
/// Tests text watermark validation.
/// </summary>
[TestClass]
internal sealed class TextWatermarkTests
{
    /// <summary>
    /// Verifies that text watermark values are retained.
    /// </summary>
    [TestMethod]
    public void ConstructorSetsProperties()
    {
        TextWatermark watermark = new("Draft", "Segoe UI", 48, 0.35, -35, 12, 24);

        Assert.AreEqual("Draft", watermark.Text);
        Assert.AreEqual("Segoe UI", watermark.FontFamily);
        Assert.AreEqual(48, watermark.FontSize);
        Assert.AreEqual(0.35, watermark.Opacity);
        Assert.AreEqual(-35, watermark.RotationDegrees);
        Assert.AreEqual(12, watermark.OffsetX);
        Assert.AreEqual(24, watermark.OffsetY);
    }

    /// <summary>
    /// Verifies that opacity is constrained to the supported range.
    /// </summary>
    [TestMethod]
    public void ConstructorRejectsInvalidOpacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TextWatermark("Draft", "Segoe UI", 48, 1.5, 0, 0, 0));
    }
}
