using System.Text;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests the XPS watermark PDL transformer.
/// </summary>
[TestClass]
internal sealed class XpsWatermarkPdlTransformerTests
{
    /// <summary>
    /// Verifies disabled watermark options leave the source stream unchanged.
    /// </summary>
    [TestMethod]
    public async Task TransformAsyncReturnsSourceWhenWatermarkIsDisabled()
    {
        MemoryStream source = new(Encoding.UTF8.GetBytes("xps"));
        TestXpsWatermarker watermarker = new(Encoding.UTF8.GetBytes("watermarked"));
        XpsWatermarkPdlTransformer transformer = new(watermarker);
        PdlPlan plan = new(PdlActionKind.Copy, PdlFormat.Oxps, PdlFormat.Oxps, null, "copy");

        Stream result = await transformer
            .TransformAsync(
                source,
                EndpointCatalog.GetByKind(EndpointKind.Xps),
                plan,
                WatermarkOptions.Disabled,
                TestContext.CancellationToken)
            .ConfigureAwait(false);

        Assert.AreSame(source, result);
        Assert.AreEqual(0, watermarker.CallCount);
    }

    /// <summary>
    /// Verifies enabled watermark options are delegated for XPS-family sources.
    /// </summary>
    [TestMethod]
    public async Task TransformAsyncAppliesWatermarkToXpsFamilySource()
    {
        byte[] sourceBytes = Encoding.UTF8.GetBytes("xps");
        byte[] watermarkedBytes = Encoding.UTF8.GetBytes("watermarked xps");
        MemoryStream source = new(sourceBytes);
        TestXpsWatermarker watermarker = new(watermarkedBytes);
        XpsWatermarkPdlTransformer transformer = new(watermarker);
        PdlPlan plan = new(PdlActionKind.Convert, PdlFormat.Oxps, PdlFormat.Pdf, PdlConversionKind.XpsToPdf, "convert");
        WatermarkOptions options = new(
            true,
            new TextWatermark("Draft", "Segoe UI", 48, 0.35, -30, 0, 0),
            null);

        Stream result = await transformer
            .TransformAsync(
                source,
                EndpointCatalog.GetByKind(EndpointKind.Pdf),
                plan,
                options,
                TestContext.CancellationToken)
            .ConfigureAwait(false);

        await using (result.ConfigureAwait(false))
        {
            using MemoryStream captured = new();
            await result.CopyToAsync(captured, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual(1, watermarker.CallCount);
            Assert.AreEqual(PdlFormat.Oxps, watermarker.LastSourceFormat);
            Assert.AreSame(options, watermarker.LastOptions);
            CollectionAssert.AreEqual(sourceBytes, watermarker.LastSourceBytes);
            CollectionAssert.AreEqual(watermarkedBytes, captured.ToArray());
        }
    }

    /// <summary>
    /// Verifies enabled watermark options reject non-XPS source formats.
    /// </summary>
    [TestMethod]
    public async Task TransformAsyncRejectsNonXpsSourceWhenWatermarkIsEnabled()
    {
        MemoryStream source = new(Encoding.UTF8.GetBytes("%PDF-1.7"));
        TestXpsWatermarker watermarker = new([]);
        XpsWatermarkPdlTransformer transformer = new(watermarker);
        PdlPlan plan = new(PdlActionKind.Copy, PdlFormat.Pdf, PdlFormat.Pdf, null, "copy");
        WatermarkOptions options = new(
            true,
            new TextWatermark("Draft", "Segoe UI", 48, 0.35, -30, 0, 0),
            null);

        await Assert
            .ThrowsExactlyAsync<NotSupportedException>(() => transformer.TransformAsync(
                source,
                EndpointCatalog.GetByKind(EndpointKind.Pdf),
                plan,
                options,
                TestContext.CancellationToken))
            .ConfigureAwait(false);

        Assert.AreEqual(0, watermarker.CallCount);
    }

    /// <summary>
    /// Gets or sets the current MSTest context.
    /// </summary>
    /// <value>The current MSTest context.</value>
    public TestContext TestContext { get; set; } = null!;
}
