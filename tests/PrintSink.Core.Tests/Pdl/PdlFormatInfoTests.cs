using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests PDL content type parsing and formatting.
/// </summary>
[TestClass]
internal sealed class PdlFormatInfoTests
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
    public void TryParseContentTypeRecognizesSupportedFormats(string contentType, PdlFormat expected)
    {
        bool parsed = PdlFormatInfo.TryParseContentType(contentType, out PdlFormat actual);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies that unsupported content types are rejected.
    /// </summary>
    [TestMethod]
    public void TryParseContentTypeRejectsUnknownFormat()
    {
        bool parsed = PdlFormatInfo.TryParseContentType("application/octet-stream", out PdlFormat _);

        Assert.IsFalse(parsed);
    }

    /// <summary>
    /// Verifies canonical content type formatting.
    /// </summary>
    [TestMethod]
    public void GetContentTypeReturnsCanonicalMediaType()
    {
        Assert.AreEqual("application/pdf", PdlFormatInfo.GetContentType(PdlFormat.Pdf));
    }

    /// <summary>
    /// Verifies maximum supported version formatting.
    /// </summary>
    [TestMethod]
    [DataRow(PdlFormat.Pdf, "1.7")]
    [DataRow(PdlFormat.PostScript, "3.0")]
    [DataRow(PdlFormat.Oxps, "1.0")]
    [DataRow(PdlFormat.Xps, "1.0")]
    [DataRow(PdlFormat.PwgRaster, "1.0")]
    [DataRow(PdlFormat.Pclm, "1.0")]
    public void GetMaxSupportedVersionReturnsManifestVersion(PdlFormat format, string expected)
    {
        Assert.AreEqual(expected, PdlFormatInfo.GetMaxSupportedVersion(format));
    }
}
