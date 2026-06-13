using PrintSink.Core.Pdl;
using PrintSink.Core.Tickets;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests physical-printer document format negotiation.
/// </summary>
[TestClass]
internal sealed class PrinterDocumentFormatSelectorTests
{
    /// <summary>
    /// Verifies that a copy-compatible default document format is preferred.
    /// </summary>
    [TestMethod]
    public void SelectPrefersCopyCompatibleDefault()
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
    public void SelectUsesDefaultWhenXpsConversionExists()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/oxps",
            "application/pdf",
            ["image/pwg-raster"]);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.AreEqual(PdlConversionKind.XpsToPdf, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that XPS can be converted to a default PCLm document format.
    /// </summary>
    [TestMethod]
    public void SelectUsesPclmDefaultWhenXpsConversionExists()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/oxps",
            "application/pclm",
            ["application/pdf"]);

        Assert.AreEqual("application/pclm", plan.TargetContentType);
        Assert.AreEqual(PdlConversionKind.XpsToPclm, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that the first supported convertible format is used when the default is not usable.
    /// </summary>
    [TestMethod]
    public void SelectUsesSupportedFormatWhenDefaultCannotBeUsed()
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
    public void SelectFallsBackToSourceContentType()
    {
        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select(
            "application/pdf",
            "image/pwg-raster",
            ["application/octet-stream"]);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.IsNull(plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that a successful IPP attribute read drives document-format selection.
    /// </summary>
    [TestMethod]
    public void SelectUsesSuccessfulIppAttributeRead()
    {
        IppAttributeReadResult attributes = IppAttributeReadResult.Success(
            new Dictionary<string, IppAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["document-format-default"] = IppAttributeValue.Create(
                    "document-format-default",
                    "image/pwg-raster"),
                ["document-format-supported"] = new IppAttributeValue(
                    "document-format-supported",
                    ["application/pdf", "image/pwg-raster"]),
            });

        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select("application/oxps", attributes);

        Assert.AreEqual("image/pwg-raster", plan.TargetContentType);
        Assert.AreEqual(PdlConversionKind.XpsToPwgRaster, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that unsupported IPP attribute reads fall back to source format submission.
    /// </summary>
    [TestMethod]
    public void SelectFallsBackWhenIppAttributeReadIsNotSupported()
    {
        IppAttributeReadResult attributes = IppAttributeReadResult.NotSupported(
            "Virtual printer attribute reads are not supported.");

        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select("application/oxps", attributes);

        Assert.AreEqual("application/oxps", plan.TargetContentType);
        Assert.IsNull(plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that failed IPP attribute reads fall back to source format submission.
    /// </summary>
    [TestMethod]
    public void SelectFallsBackWhenIppAttributeReadFails()
    {
        IppAttributeReadResult attributes = IppAttributeReadResult.Failed("Printer attribute query failed.");

        PrinterDocumentFormatPlan plan = PrinterDocumentFormatSelector.Select("application/pdf", attributes);

        Assert.AreEqual("application/pdf", plan.TargetContentType);
        Assert.IsNull(plan.ConversionKind);
    }
}
