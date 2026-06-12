using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests PDL content type parsing and formatting.
/// </summary>
[TestClass]
public sealed class PdlFormatInfoTests
{
    /// <summary>
    /// Verifies parsing for supported content types.
    /// </summary>
    [TestMethod]
    [DataRow("application/oxps", PdlFormat.Oxps)]
    [DataRow("application/vnd.ms-xpsdocument", PdlFormat.Xps)]
    [DataRow("application/pdf", PdlFormat.Pdf)]
    [DataRow("application/postscript", PdlFormat.PostScript)]
    [DataRow("image/pwg-raster", PdlFormat.PwgRaster)]
    [DataRow("application/pclm", PdlFormat.Pclm)]
    [DataRow("APPLICATION/PDF; version=1.7", PdlFormat.Pdf)]
    public void TryParseContentType_recognizes_supported_formats(string contentType, PdlFormat expected)
    {
        bool parsed = PdlFormatInfo.TryParseContentType(contentType, out PdlFormat actual);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies that unsupported content types are rejected.
    /// </summary>
    [TestMethod]
    public void TryParseContentType_rejects_unknown_format()
    {
        bool parsed = PdlFormatInfo.TryParseContentType("application/octet-stream", out PdlFormat _);

        Assert.IsFalse(parsed);
    }

    /// <summary>
    /// Verifies canonical content type formatting.
    /// </summary>
    [TestMethod]
    public void GetContentType_returns_canonical_media_type()
    {
        Assert.AreEqual("application/pdf", PdlFormatInfo.GetContentType(PdlFormat.Pdf));
    }
}
