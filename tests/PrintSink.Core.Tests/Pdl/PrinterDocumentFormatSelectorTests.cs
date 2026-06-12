using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests physical-printer document format negotiation.
/// </summary>
[TestClass]
public sealed class PrinterDocumentFormatSelectorTests
{
    /// <summary>
    /// Verifies that a copy-compatible default document format is preferred.
    /// </summary>
    [TestMethod]
    public void Select_prefers_copy_compatible_default()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/pdf",
            "application/pdf",
            ["image/pwg-raster"]);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.IsNull(plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that XPS can be converted to a default PDF document format.
    /// </summary>
    [TestMethod]
    public void Select_uses_default_when_xps_conversion_exists()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/oxps",
            "application/pdf",
            ["image/pwg-raster"]);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that the first supported convertible format is used when the default is not usable.
    /// </summary>
    [TestMethod]
    public void Select_uses_supported_format_when_default_cannot_be_used()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/oxps",
            "application/postscript",
            ["application/octet-stream", "image/pwg-raster", "application/pdf"]);

        Assert.AreEqual("image/pwg-raster", plan.TargetContentType);
        Assert.AreEqual(PdlConversionKind.XpsToPwgRaster, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that the source content type is retained when no printer format can be selected.
    /// </summary>
    [TestMethod]
    public void Select_falls_back_to_source_content_type()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/pdf",
            "image/pwg-raster",
            ["application/octet-stream"]);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.IsNull(plan.ConversionKind);
    }
}
