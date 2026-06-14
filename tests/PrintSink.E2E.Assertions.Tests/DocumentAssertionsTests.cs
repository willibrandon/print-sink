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
    /// Verifies a valid OXPS package with interleaved fixed-page pieces is accepted.
    /// </summary>
    [TestMethod]
    public void RunAcceptsInterleavedOxpsWithExpectedText()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "interleaved.oxps");
            WriteInterleavedXpsPackage(path);

            int exitCode = RunAssertion(["--format", "oxps", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(0, exitCode, error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies an OXPS package without a fixed representation relationship is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsOxpsWithoutFixedRepresentationRelationship()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "missing-fixed-representation.oxps");
            WriteXpsPackage(path, "foo", includeFixedRepresentationRelationship: false);

            int exitCode = RunAssertion(["--format", "oxps", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("XPS package is missing a fixed representation relationship", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies an OXPS package with an unreachable fixed page is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsOxpsWithOrphanFixedPage()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "orphan-fixed-page.oxps");
            WriteXpsPackage(path, "foo", pageReference: "Pages/missing.fpage");

            int exitCode = RunAssertion(["--format", "oxps", "--path", path, "--contains", "foo"], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("XPS fixed document references missing fixed page", error);
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
    /// Verifies an OXPS package with expected text only in a comment is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsOxpsWithExpectedTextOnlyInComment()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "comment-text.oxps");
            WriteXpsPackage(path, "bar", commentText: "foo");

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
    /// Verifies PostScript with a nonnumeric page count is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsInvalidPostScriptPageCount()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "invalid-pages.ps");
            WritePostScript(path, "foo", "many");

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
    /// Verifies PostScript with a resolved page count that does not match page records is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsMismatchedPostScriptPageCount()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "mismatched-pages.ps");
            WritePostScript(path, "foo", "2");

            int exitCode = RunAssertion(["--format", "postscript", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("resolved page count 2 does not match 1 page record", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies PostScript with a nonnumeric bounding box is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsInvalidPostScriptBoundingBox()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "invalid-bounding-box.ps");
            WritePostScript(path, "foo", "1", boundingBox: "left bottom right top");

            int exitCode = RunAssertion(["--format", "postscript", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("does not contain a resolved bounding box", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies PostScript with executable content after EOF is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsPostScriptContentAfterEof()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "content-after-eof.ps");
            WritePostScript(path, "foo", "1");
            File.AppendAllText(path, "showpage\n", Encoding.Latin1);

            int exitCode = RunAssertion(["--format", "postscript", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("missing required DSC closing markers", error);
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
    /// Verifies a PWG Raster stream shorter than its declared page body is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsTruncatedPwgRaster()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "truncated.pwg");
            WritePwgRaster(path, blankBody: false, magic: "RaS3", height: 128, bodyLength: 16);

            int exitCode = RunAssertion(["--format", "pwg", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PWG Raster page body is shorter than declared page data", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a compressed PWG Raster stream with a truncated scan line is rejected.
    /// </summary>
    [TestMethod]
    public void RunRejectsTruncatedCompressedPwgRaster()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "truncated-compressed.pwg");
            WritePwgRaster(path, blankBody: false, truncateCompressedBody: true);

            int exitCode = RunAssertion(["--format", "pwg", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PWG Raster compressed scan line is truncated", error);
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
            Assert.Contains("PCLm page 1 did not contain image content", error);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies a PCLm document is rejected when any page has no raster image.
    /// </summary>
    [TestMethod]
    public void RunRejectsPclmWithBlankPage()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "blank-page.pclm");
            WriteMinimalImagePdf(
                path,
                includePclmMarker: true,
                includeSecondPage: true,
                includeSecondPageImage: false);

            int exitCode = RunAssertion(["--format", "pclm", "--path", path], out string error);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("PCLm page 2 did not contain image content", error);
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
        bool includeCommentOnlyImageMarker = false,
        bool includeSecondPage = false,
        bool includeSecondPageImage = true)
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

        if (includeSecondPage && includeSecondPageImage && !includeImage)
        {
            throw new ArgumentException("Second-page image fixtures require the shared image object.", nameof(includeSecondPageImage));
        }

        int secondPageObjectNumber = includeImage ? 6 : 4;
        int secondPageContentObjectNumber = includeImage ? 7 : 5;
        string pageKids = includeSecondPage
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"3 0 R {secondPageObjectNumber} 0 R")
            : "3 0 R";
        int pageCount = includeSecondPage ? 2 : 1;
        AppendPdfObject(builder, offsets, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        AppendPdfObject(
            builder,
            offsets,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"2 0 obj\n<< /Type /Pages /Count {pageCount} /Kids [{pageKids}] >>\nendobj\n"));
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

        if (includeSecondPage)
        {
            if (includeSecondPageImage)
            {
                AppendPdfObject(
                    builder,
                    offsets,
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{secondPageObjectNumber} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 4 0 R >> >> /Contents {secondPageContentObjectNumber} 0 R >>\nendobj\n"));
                const string secondPageContentStream = "q\n1 0 0 1 144 720 cm\n/Im0 Do\nQ\n";
                AppendPdfObject(
                    builder,
                    offsets,
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{secondPageContentObjectNumber} 0 obj\n<< /Length {Encoding.Latin1.GetByteCount(secondPageContentStream)} >>\nstream\n{secondPageContentStream}endstream\nendobj\n"));
            }
            else
            {
                AppendPdfObject(
                    builder,
                    offsets,
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{secondPageObjectNumber} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n"));
            }
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

    private static void WriteXpsPackage(
        string path,
        string fixedPageText,
        string? commentText = null,
        bool includeFixedRepresentationRelationship = true,
        string pageReference = "Pages/1.fpage")
    {
        string comment = string.IsNullOrWhiteSpace(commentText)
            ? string.Empty
            : $"  <!-- {commentText} -->";
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteXpsContentTypes(archive);
        WriteXpsPackageGraph(archive, includeFixedRepresentationRelationship, pageReference);
        WriteZipEntry(
            archive,
            "Documents/1/Pages/1.fpage",
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FixedPage xmlns="http://schemas.microsoft.com/xps/2005/06" Width="816" Height="1056">
            {comment}
              <Glyphs UnicodeString="{fixedPageText}" />
            </FixedPage>
            """);
    }

    private static void WriteInterleavedXpsPackage(string path)
    {
        const string fixedPageStart =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <FixedPage xmlns="http://schemas.microsoft.com/xps/2005/06" Width="816" Height="1056">
              <Glyphs UnicodeString="fo
            """;
        const string fixedPageEnd =
            """
            o" />
            </FixedPage>
            """;

        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteXpsContentTypes(archive, includeInterleavedPiece: true);
        WriteXpsPackageGraph(archive);
        WriteZipEntry(archive, "Documents/1/Pages/1.fpage/[1].piece", fixedPageEnd, emitBom: false);
        WriteZipEntry(archive, "Documents/1/Pages/1.fpage/[0].piece", fixedPageStart, emitBom: true);
    }

    private static void WriteXpsContentTypes(ZipArchive archive, bool includeInterleavedPiece = false)
    {
        string pieceContentType = includeInterleavedPiece
            ? """
                <Default Extension="piece" ContentType="application/vnd.ms-package.interleaved-part" />
            """
            : string.Empty;
        WriteZipEntry(
            archive,
            "[Content_Types].xml",
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="fdseq" ContentType="application/vnd.ms-package.xps-fixeddocumentsequence+xml" />
              <Default Extension="fdoc" ContentType="application/vnd.ms-package.xps-fixeddocument+xml" />
              <Default Extension="fpage" ContentType="application/vnd.ms-package.xps-fixedpage+xml" />
            {{pieceContentType}}</Types>
            """);
    }

    private static void WriteXpsPackageGraph(
        ZipArchive archive,
        bool includeFixedRepresentationRelationship = true,
        string pageReference = "Pages/1.fpage")
    {
        string packageRelationship = includeFixedRepresentationRelationship
            ? """
                <Relationship Target="FixedDocumentSequence.fdseq" Id="R0" Type="http://schemas.openxps.org/oxps/v1.0/fixedrepresentation" />
            """
            : """
                <Relationship Target="DiscardControl.xml" Id="R0" Type="http://schemas.openxps.org/oxps/v1.0/discard-control" />
            """;
        WriteZipEntry(
            archive,
            "_rels/.rels",
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            {{packageRelationship}}</Relationships>
            """);
        WriteZipEntry(
            archive,
            "FixedDocumentSequence.fdseq",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <FixedDocumentSequence xmlns="http://schemas.openxps.org/oxps/v1.0">
              <DocumentReference Source="Documents/1/FixedDocument.fdoc" />
            </FixedDocumentSequence>
            """);
        WriteZipEntry(
            archive,
            "Documents/1/FixedDocument.fdoc",
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <FixedDocument xmlns="http://schemas.openxps.org/oxps/v1.0">
              <PageContent Source="{{pageReference}}" />
            </FixedDocument>
            """);
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string text, bool emitBom = true)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom));
        writer.Write(text);
    }

    private static void WritePostScript(string path, string text, string pagesValue, string boundingBox = "0 0 612 792")
    {
        File.WriteAllText(
            path,
            $"""
            %!PS-Adobe-3.0
            %%Pages: {pagesValue}
            %%BoundingBox: {boundingBox}
            %%Page: 1 1
            ({text}) show
            %%PageTrailer
            %%Trailer
            %%EOF
            """,
            Encoding.Latin1);
    }

    private static void WritePwgRaster(
        string path,
        bool blankBody,
        string magic = "RaS2",
        uint height = 2,
        int bodyLength = 16,
        bool truncateCompressedBody = false)
    {
        const int syncWordLength = 4;
        const int version2HeaderLength = 1796;
        bool isCompressed = magic is "RaS2" or "2SaR";
        byte[] body = isCompressed
            ? CreateCompressedPwgRasterBody(blankBody, height, truncateCompressedBody)
            : new byte[bodyLength];
        byte[] bytes = new byte[syncWordLength + version2HeaderLength + body.Length];
        Encoding.ASCII.GetBytes(magic).CopyTo(bytes, 0);
        WriteRasterUInt32(bytes, syncWordLength + 372, 2);
        WriteRasterUInt32(bytes, syncWordLength + 376, height);
        WriteRasterUInt32(bytes, syncWordLength + 384, 8);
        WriteRasterUInt32(bytes, syncWordLength + 388, 8);
        WriteRasterUInt32(bytes, syncWordLength + 392, 2);
        WriteRasterUInt32(bytes, syncWordLength + 396, 0);
        WriteRasterUInt32(bytes, syncWordLength + 400, 3);
        WriteRasterUInt32(bytes, syncWordLength + 420, 1);

        body.CopyTo(bytes, syncWordLength + version2HeaderLength);

        if (!isCompressed && !blankBody)
        {
            if (bodyLength > 0)
            {
                bytes[syncWordLength + version2HeaderLength] = 0x00;
            }

            if (bodyLength > 1)
            {
                bytes[syncWordLength + version2HeaderLength + 1] = 0xFF;
            }
        }

        File.WriteAllBytes(path, bytes);
    }

    private static byte[] CreateCompressedPwgRasterBody(bool blankBody, uint height, bool truncate)
    {
        List<byte> body = [];
        uint remainingRows = height;
        while (remainingRows > 0)
        {
            int repeatCount = (int)Math.Min(remainingRows, 256);
            body.Add((byte)(repeatCount - 1));
            if (blankBody)
            {
                body.Add(1);
                body.Add(0);
            }
            else
            {
                body.Add(255);
                body.Add(0);
                body.Add(255);
            }

            remainingRows -= (uint)repeatCount;
        }

        if (truncate)
        {
            body.RemoveAt(body.Count - 1);
        }

        return [.. body];
    }

    private static void WriteRasterUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
    }
}
