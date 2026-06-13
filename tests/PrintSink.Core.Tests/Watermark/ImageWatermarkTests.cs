using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Watermark;

/// <summary>
/// Tests image watermark validation.
/// </summary>
[TestClass]
internal sealed class ImageWatermarkTests
{
    /// <summary>
    /// Verifies that image watermark values are retained.
    /// </summary>
    [TestMethod]
    public void ConstructorSetsProperties()
    {
        ImageWatermark watermark = new("C:\\Watermarks\\logo.png", 144, 96, 0.4, 15, 12, 24);

        Assert.AreEqual("C:\\Watermarks\\logo.png", watermark.ImagePath);
        Assert.AreEqual(144, watermark.Width);
        Assert.AreEqual(96, watermark.Height);
        Assert.AreEqual(0.4, watermark.Opacity);
        Assert.AreEqual(15, watermark.RotationDegrees);
        Assert.AreEqual(12, watermark.OffsetX);
        Assert.AreEqual(24, watermark.OffsetY);
    }

    /// <summary>
    /// Verifies that width is constrained to positive values.
    /// </summary>
    [TestMethod]
    public void ConstructorRejectsInvalidWidth()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageWatermark("C:\\Watermarks\\logo.png", 0, 96, 0.4, 0, 0, 0));
    }

    /// <summary>
    /// Verifies that opacity is constrained to the supported range.
    /// </summary>
    [TestMethod]
    public void ConstructorRejectsInvalidOpacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageWatermark("C:\\Watermarks\\logo.png", 144, 96, -0.1, 0, 0, 0));
    }
}
