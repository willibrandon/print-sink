using PrintSink.E2E.Assertions;
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
}
