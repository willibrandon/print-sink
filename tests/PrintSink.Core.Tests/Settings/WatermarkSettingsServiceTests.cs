using PrintSink.Settings;
using PrintSink.Watermark;

namespace PrintSink.Core.Tests.Settings;

/// <summary>
/// Tests for <see cref="WatermarkSettingsService"/>.
/// </summary>
[TestClass]
public sealed class WatermarkSettingsServiceTests
{
    /// <summary>
    /// Gets or sets the MSTest context for the current test.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies enabled watermark options round-trip through the settings store.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task SaveAndLoadAsync_EnabledOptions_RoundTrips()
    {
        InMemorySettingsStore store = new();
        WatermarkSettingsService service = new(store);
        WatermarkOptions expected = new(
            isTextEnabled: true,
            text: new TextWatermark("Draft", 36, 0.4, -30, 12, 24),
            isImageEnabled: true,
            image: new ImageWatermark("Assets/watermark.png", 96, 96, 128, 64, 0.5));

        await service.SaveAsync(expected, TestContext.CancellationToken);
        WatermarkOptions actual = await service.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(actual.IsEnabled);
        Assert.IsNotNull(actual.Text);
        Assert.IsNotNull(actual.Image);
        Assert.AreEqual("Draft", actual.Text.Text);
        Assert.AreEqual("Assets/watermark.png", actual.Image.Path);
    }

    /// <summary>
    /// Verifies disabled options remove the stored value.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task SaveAsync_DisabledOptions_RemovesStoredValue()
    {
        InMemorySettingsStore store = new();
        WatermarkSettingsService service = new(store);

        await service.SaveAsync(WatermarkOptions.Disabled, TestContext.CancellationToken);
        WatermarkOptions actual = await service.LoadAsync(TestContext.CancellationToken);

        Assert.IsFalse(actual.IsEnabled);
    }
}
