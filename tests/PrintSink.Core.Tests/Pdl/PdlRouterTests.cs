using PrintSink.Endpoints;
using PrintSink.Pdl;
using PrintSink.Watermark;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests for <see cref="PdlRouter"/>.
/// </summary>
[TestClass]
public sealed class PdlRouterTests
{
    /// <summary>
    /// Verifies OXPS to PDF conversion with pre-conversion watermarking.
    /// </summary>
    [TestMethod]
    public void Resolve_OxpsToPdfWithWatermark_ReturnsXpsToPdfPlan()
    {
        PdlRouter router = new();
        WatermarkOptions watermark = new(
            isTextEnabled: true,
            text: new TextWatermark("Confidential", 48, 0.35, -35, 0, 0),
            isImageEnabled: false,
            image: null);

        PdlPlan plan = router.Resolve(PdlFormatInfo.OxpsContentType, EndpointCatalog.Pdf, watermark);

        Assert.AreEqual(PdlActionKind.Convert, plan.Action);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, plan.Conversion);
        Assert.AreEqual(PdlFormat.Oxps, plan.SourceFormat);
        Assert.AreEqual(PdlFormat.Pdf, plan.TargetFormat);
        Assert.IsTrue(plan.RequiresWatermark);
    }

    /// <summary>
    /// Verifies PDF passthrough for the PDF endpoint.
    /// </summary>
    [TestMethod]
    public void Resolve_PdfPassthrough_ReturnsCopyPlan()
    {
        PdlRouter router = new();

        PdlPlan plan = router.Resolve(PdlFormatInfo.PdfContentType, EndpointCatalog.Pdf, WatermarkOptions.Disabled);

        Assert.AreEqual(PdlActionKind.Copy, plan.Action);
        Assert.AreEqual(PdlConversionKind.None, plan.Conversion);
        Assert.AreEqual(PdlFormat.Pdf, plan.SourceFormat);
        Assert.AreEqual(PdlFormat.Pdf, plan.TargetFormat);
        Assert.IsFalse(plan.RequiresWatermark);
    }

    /// <summary>
    /// Verifies PostScript passthrough for the PostScript endpoint.
    /// </summary>
    [TestMethod]
    public void Resolve_PostScriptPassthrough_ReturnsCopyPlan()
    {
        PdlRouter router = new();

        PdlPlan plan = router.Resolve(PdlFormatInfo.PostScriptContentType, EndpointCatalog.PostScript);

        Assert.AreEqual(PdlActionKind.Copy, plan.Action);
        Assert.AreEqual(PdlFormat.PostScript, plan.SourceFormat);
        Assert.AreEqual(PdlFormat.PostScript, plan.TargetFormat);
    }

    /// <summary>
    /// Verifies OXPS to PWG Raster conversion.
    /// </summary>
    [TestMethod]
    public void Resolve_OxpsToPwgRaster_ReturnsXpsToPwgrPlan()
    {
        PdlRouter router = new();

        PdlPlan plan = router.Resolve(PdlFormatInfo.OxpsContentType, EndpointCatalog.PwgRaster);

        Assert.AreEqual(PdlActionKind.Convert, plan.Action);
        Assert.AreEqual(PdlConversionKind.XpsToPwgr, plan.Conversion);
        Assert.AreEqual(PdlFormat.PwgRaster, plan.TargetFormat);
    }

    /// <summary>
    /// Verifies OXPS to PCLm conversion for custom-file endpoints.
    /// </summary>
    [TestMethod]
    public void Resolve_OxpsToPclm_ReturnsXpsToPclmPlan()
    {
        PdlRouter router = new();

        PdlPlan plan = router.Resolve(PdlFormatInfo.OxpsContentType, EndpointCatalog.Pclm);

        Assert.AreEqual(PdlActionKind.Convert, plan.Action);
        Assert.AreEqual(PdlConversionKind.XpsToPclm, plan.Conversion);
        Assert.AreEqual(PdlFormat.Pclm, plan.TargetFormat);
    }

    /// <summary>
    /// Verifies unsupported source content is rejected deterministically.
    /// </summary>
    [TestMethod]
    public void Resolve_UnsupportedContentType_ReturnsRejectPlan()
    {
        PdlRouter router = new();

        PdlPlan plan = router.Resolve("application/vnd.example.unknown", EndpointCatalog.Pdf);

        Assert.AreEqual(PdlActionKind.Reject, plan.Action);
        Assert.AreEqual(PdlFormat.Unknown, plan.SourceFormat);
        Assert.IsFalse(string.IsNullOrWhiteSpace(plan.RejectionReason));
    }
}
