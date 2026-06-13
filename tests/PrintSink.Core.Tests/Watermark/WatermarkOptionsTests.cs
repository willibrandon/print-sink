using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Watermark;

/// <summary>
/// Tests watermark option validation.
/// </summary>
[TestClass]
internal sealed class WatermarkOptionsTests
{
    /// <summary>
    /// Verifies that disabled watermark options contain no watermark payload.
    /// </summary>
    [TestMethod]
    public void DisabledReturnsDisabledOptions()
    {
        WatermarkOptions options = WatermarkOptions.Disabled;

        Assert.IsFalse(options.Enabled);
        Assert.IsNull(options.Text);
        Assert.IsNull(options.Image);
    }

    /// <summary>
    /// Verifies that enabled options require a configured watermark.
    /// </summary>
    [TestMethod]
    public void ConstructorRejectsEnabledOptionsWithoutPayload()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WatermarkOptions(true, null, null));
    }

    /// <summary>
    /// Verifies that enabled options can carry only an image watermark.
    /// </summary>
    [TestMethod]
    public void ConstructorAcceptsImageOnlyOptions()
    {
        ImageWatermark image = new("C:\\Watermarks\\logo.png", 144, 96, 0.4, 0, 0, 0);
        WatermarkOptions options = new(true, null, image);

        Assert.IsTrue(options.Enabled);
        Assert.IsNull(options.Text);
        Assert.AreSame(image, options.Image);
    }
}
