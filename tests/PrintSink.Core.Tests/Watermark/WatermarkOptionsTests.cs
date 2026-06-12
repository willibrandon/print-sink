using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Watermark;

/// <summary>
/// Tests watermark option validation.
/// </summary>
[TestClass]
public sealed class WatermarkOptionsTests
{
    /// <summary>
    /// Verifies that disabled watermark options contain no watermark payload.
    /// </summary>
    [TestMethod]
    public void Disabled_returns_disabled_options()
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
    public void Constructor_rejects_enabled_options_without_payload()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WatermarkOptions(true, null, null));
    }
}
