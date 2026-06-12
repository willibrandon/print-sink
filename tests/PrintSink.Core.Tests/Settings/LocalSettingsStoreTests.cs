using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Settings;

/// <summary>
/// Tests local settings persistence.
/// </summary>
[TestClass]
public sealed class LocalSettingsStoreTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies missing watermark settings return disabled options.
    /// </summary>
    [TestMethod]
    public async Task GetWatermarkOptionsAsync_returns_disabled_when_missing()
    {
        string directory = CreateTestDirectory();
        LocalSettingsStore store = new(directory);

        try
        {
            WatermarkOptions options = await store
                .GetWatermarkOptionsAsync(new Uri("ipp://localhost/printsink/pdf"), TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsFalse(options.Enabled);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies watermark options round-trip through local JSON storage.
    /// </summary>
    [TestMethod]
    public async Task SaveWatermarkOptionsAsync_round_trips_text_watermark()
    {
        string directory = CreateTestDirectory();
        LocalSettingsStore store = new(directory);
        Uri printerUri = new("ipp://localhost/printsink/pdf");
        WatermarkOptions expected = new(
            true,
            new TextWatermark("Confidential", "Segoe UI", 42, 0.25, -30, 10, 20),
            null);

        try
        {
            await store
                .SaveWatermarkOptionsAsync(printerUri, expected, TestContext.CancellationToken)
                .ConfigureAwait(false);

            WatermarkOptions actual = await store
                .GetWatermarkOptionsAsync(printerUri, TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsTrue(actual.Enabled);
            Assert.IsNotNull(actual.Text);
            Assert.AreEqual("Confidential", actual.Text.Text);
            Assert.AreEqual("Segoe UI", actual.Text.FontFamily);
            Assert.AreEqual(42, actual.Text.FontSize);
            Assert.AreEqual(0.25, actual.Text.Opacity);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies settings are partitioned by printer URI.
    /// </summary>
    [TestMethod]
    public async Task SaveWatermarkOptionsAsync_partitions_by_printer_uri()
    {
        string directory = CreateTestDirectory();
        LocalSettingsStore store = new(directory);

        try
        {
            await store
                .SaveWatermarkOptionsAsync(
                    new Uri("ipp://localhost/printsink/pdf"),
                    new WatermarkOptions(true, new TextWatermark("PDF", "Segoe UI", 36, 0.2, 0, 0, 0), null),
                    TestContext.CancellationToken)
                .ConfigureAwait(false);

            WatermarkOptions xpsOptions = await store
                .GetWatermarkOptionsAsync(new Uri("ipp://localhost/printsink/xps"), TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsFalse(xpsOptions.Enabled);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PrintSink.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }
}
