using PrintSink.E2E.Assertions;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PrintSink.E2E.Assertions.Tests;

/// <summary>
/// Tests the document assertion executable used by the live print-stack E2E suite.
/// </summary>
[TestClass]
internal sealed class DocumentAssertionsTests
{
    /// <summary>
    /// Verifies a valid PDF with expected text is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsValidPdfWithExpectedText()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "valid.pdf");
            WritePdf(path, "foo");

            int exitCode = RunAssertion(["--format", "pdf", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies malformed PDF bytes are rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsMalformedPdf()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "malformed.pdf");
            File.WriteAllText(path, "%PDF-1.7 not a complete document");

            int exitCode = RunAssertion(["--format", "pdf", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("Could not find", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies empty outputs are rejected before parser-specific validation.
    /// </summary>
    [TestMethod]
    public void RunRejectsEmptyOutput()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "empty.pdf");
            File.WriteAllBytes(path, []);

            int exitCode = RunAssertion(["--format", "pdf", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("Output file is empty", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies valid PDFs still fail when expected text is missing.
    /// </summary>
    [TestMethod]
    public void RunRejectsPdfMissingExpectedText()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "wrong-text.pdf");
            WritePdf(path, "bar");

            int exitCode = RunAssertion(["--format", "pdf", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PDF text did not contain 'foo'", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a valid OXPS package with expected fixed-page text is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsValidOxpsWithExpectedText()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "valid.oxps");
            WriteXpsPackage(path, "foo");

            int exitCode = RunAssertion(["--format", "oxps", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies an OXPS package with missing expected text is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsOxpsMissingExpectedText()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "missing-text.oxps");
            WriteXpsPackage(path, "bar");

            int exitCode = RunAssertion(["--format", "oxps", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("XPS fixed pages did not contain 'foo'", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a valid PostScript document is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsValidPostScript()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "valid.ps");
            WritePostScript(path, "foo", "1");

            int exitCode = RunAssertion(["--format", "postscript", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies PostScript with deferred page count markers is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsUnresolvedPostScriptPageCount()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "unresolved.ps");
            WritePostScript(path, "foo", "(atend)");

            int exitCode = RunAssertion(["--format", "postscript", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("does not contain a resolved page count", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a valid PWG Raster byte stream is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsValidPwgRaster()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "valid.pwg");
            WritePwgRaster(path, blankBody: false);

            int exitCode = RunAssertion(["--format", "pwg", "--path", path], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a structurally valid but blank PWG Raster page is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsBlankPwgRaster()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "blank.pwg");
            WritePwgRaster(path, blankBody: true);

            int exitCode = RunAssertion(["--format", "pwg", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PWG Raster page body appears blank", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a PDF/PCLm document with image content is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsValidPclm()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "valid.pclm");
            WriteMinimalImagePdf(path, includePclmMarker: true);

            int exitCode = RunAssertion(["--format", "pclm", "--path", path], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a PDF without the PCLm header marker is rejected as PCLm.
    /// </summary>
    [TestMethod]
    public void RunRejectsPclmMissingHeaderMarker()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "not-pclm.pdf");
            WriteMinimalImagePdf(path, includePclmMarker: false);

            int exitCode = RunAssertion(["--format", "pclm", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PCLm output is missing the PDF/PCLm header markers", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a PCLm output is rejected when image evidence only appears in a PDF comment.
    /// </summary>
    [TestMethod]
    public void RunRejectsPclmCommentOnlyImageMarker()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "comment-only.pclm");
            WriteMinimalImagePdf(path, includePclmMarker: true, includeImage: false, includeCommentOnlyImageMarker: true);

            int exitCode = RunAssertion(["--format", "pclm", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PDF did not contain image content", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static int RunAssertion(string[] args, out string error)
    {
        using StringWriter outputWriter = new();
        using StringWriter errorWriter = new();
        int exitCode = DocumentAssertions.Run(args, outputWriter, errorWriter);
        error = errorWriter.ToString();
        return exitCode;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PrintSink.E2E.Assertions.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void WritePdf(string path, string text)
    {
        using PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        PdfPageBuilder page = builder.AddPage(PageSize.Letter);
        page.AddText(text, 12, new PdfPoint(72, 720), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteMinimalImagePdf(
        string path,
        bool includePclmMarker,
        bool includeImage = true,
        bool includeCommentOnlyImageMarker = false)
    {
        StringBuilder builder = new();
        List<int> offsets = [];
        builder.Append("%PDF-1.7\n");
        if (includePclmMarker)
        {
            builder.Append("%PCLm 1.0\n");
        }

        if (includeCommentOnlyImageMarker)
        {
            builder.Append("%/Subtype/Image\n");
        }

        AppendPdfObject(builder, offsets, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        AppendPdfObject(builder, offsets, "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n");
        if (includeImage)
        {
            AppendPdfObject(
                builder,
                offsets,
                "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
            AppendPdfObject(
                builder,
                offsets,
                "4 0 obj\n<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Length 1 >>\nstream\n\u0080\nendstream\nendobj\n");
            const string contentStream = "q\n1 0 0 1 72 720 cm\n/Im0 Do\nQ\n";
            AppendPdfObject(
                builder,
                offsets,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"5 0 obj\n<< /Length {Encoding.Latin1.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream\nendobj\n"));
        }
        else
        {
            AppendPdfObject(builder, offsets, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n");
        }

        int xrefOffset = builder.Length;
        builder.Append("xref\n0 ");
        builder.Append((offsets.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            builder.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Root 1 0 R /Size ");
        builder.Append((offsets.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(" >>\nstartxref\n");
        builder.Append(xrefOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("\n%%EOF\n");
        File.WriteAllText(path, builder.ToString(), Encoding.Latin1);
    }

    private static void AppendPdfObject(StringBuilder builder, List<int> offsets, string text)
    {
        offsets.Add(builder.Length);
        builder.Append(text);
    }

    private static void WriteXpsPackage(string path, string fixedPageText)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteZipEntry(
            archive,
            "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="fdseq" ContentType="application/vnd.ms-package.xps-fixeddocumentsequence+xml" />
              <Default Extension="fdoc" ContentType="application/vnd.ms-package.xps-fixeddocument+xml" />
              <Default Extension="fpage" ContentType="application/vnd.ms-package.xps-fixedpage+xml" />
            </Types>
            """);
        WriteZipEntry(
            archive,
            "Documents/1/Pages/1.fpage",
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FixedPage xmlns="http://schemas.microsoft.com/xps/2005/06" Width="816" Height="1056">
              <Glyphs UnicodeString="{fixedPageText}" />
            </FixedPage>
            """);
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        writer.Write(text);
    }

    private static void WritePostScript(string path, string text, string pagesValue)
    {
        File.WriteAllText(
            path,
            $"""
            %!PS-Adobe-3.0
            %%Pages: {pagesValue}
            %%BoundingBox: 0 0 612 792
            %%Page: 1 1
            ({text}) show
            %%PageTrailer
            %%Trailer
            %%EOF
            """,
            Encoding.Latin1);
    }

    private static void WritePwgRaster(string path, bool blankBody)
    {
        const int syncWordLength = 4;
        const int version2HeaderLength = 1796;
        const int bodyLength = 16;
        byte[] bytes = new byte[syncWordLength + version2HeaderLength + bodyLength];
        Encoding.ASCII.GetBytes("RaS2").CopyTo(bytes, 0);
        WriteRasterUInt32(bytes, syncWordLength + 372, 2);
        WriteRasterUInt32(bytes, syncWordLength + 376, 2);
        WriteRasterUInt32(bytes, syncWordLength + 384, 8);
        WriteRasterUInt32(bytes, syncWordLength + 388, 8);
        WriteRasterUInt32(bytes, syncWordLength + 392, 2);
        WriteRasterUInt32(bytes, syncWordLength + 396, 0);
        WriteRasterUInt32(bytes, syncWordLength + 400, 3);
        WriteRasterUInt32(bytes, syncWordLength + 420, 1);

        if (!blankBody)
        {
            bytes[syncWordLength + version2HeaderLength] = 0x00;
            bytes[syncWordLength + version2HeaderLength + 1] = 0xFF;
        }

        File.WriteAllBytes(path, bytes);
    }

    private static void WriteRasterUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
    }
}
