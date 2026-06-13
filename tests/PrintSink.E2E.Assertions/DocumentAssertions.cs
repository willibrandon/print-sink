using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PrintSink.E2E.Assertions;

internal static class DocumentAssertions
{
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            Dictionary<string, string> options = ParseOptions(args);
            string format = GetRequired(options, "format");
            string path = GetRequired(options, "path");
            options.TryGetValue("contains", out string? expectedText);
            options.TryGetValue("not-contains", out string? forbiddenText);
            bool requireImage = GetOptionalBoolean(options, "requires-image");

            AssertDocument(format, path, expectedText, forbiddenText, requireImage);
            output.WriteLine($"ok: {format} output is valid: {path}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or PdfDocumentFormatException)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void AssertDocument(
        string format,
        string path,
        string? expectedText,
        string? forbiddenText,
        bool requireImage)
    {
        FileInfo file = new(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException($"Output file was not written: {path}", path);
        }

        if (file.Length == 0)
        {
            throw new InvalidDataException($"Output file is empty: {path}");
        }

        switch (format.ToLowerInvariant())
        {
            case "pdf":
                AssertPdf(path, expectedText, forbiddenText, true, requireImage);
                break;
            case "xps":
            case "oxps":
                AssertXps(path, expectedText, forbiddenText);
                break;
            case "postscript":
            case "ps":
                AssertPostScript(path, expectedText, forbiddenText);
                break;
            case "pwg":
            case "pwgraster":
                AssertPwgRaster(path);
                break;
            case "pclm":
                AssertPdf(path, null, forbiddenText, false, false);
                break;
            default:
                throw new ArgumentException($"Unsupported document format '{format}'.");
        }
    }

    private static void AssertPdf(
        string path,
        string? expectedText,
        string? forbiddenText,
        bool requireText,
        bool requireImage)
    {
        using PdfDocument document = PdfDocument.Open(path);
        if (document.NumberOfPages < 1)
        {
            throw new InvalidDataException($"PDF has no pages: {path}");
        }

        string text = ExtractPdfText(document);
        if (requireText && string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException($"PDF text extraction produced no text: {path}");
        }

        if (!string.IsNullOrWhiteSpace(expectedText)
            && !text.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"PDF text did not contain '{expectedText}'. Extracted text: {text}");
        }

        if (!string.IsNullOrWhiteSpace(forbiddenText)
            && text.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("PDF text contained a forbidden value.");
        }

        if (requireImage && !ContainsPdfImage(document, path))
        {
            throw new InvalidDataException($"PDF did not contain image content: {path}");
        }
    }

    private static string ExtractPdfText(PdfDocument document)
    {
        StringBuilder text = new();
        foreach (Page page in document.GetPages())
        {
            string pageText = ContentOrderTextExtractor.GetText(page);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                pageText = page.Text;
            }

            text.AppendLine(pageText);
        }

        return text.ToString();
    }

    private static bool ContainsPdfImage(PdfDocument document, string path)
    {
        foreach (Page page in document.GetPages())
        {
            if (page.NumberOfImages > 0 || page.GetImages().Any())
            {
                return true;
            }
        }

        string pdfSource = File.ReadAllText(path, Encoding.Latin1);
        return pdfSource.Contains("/Subtype/Image", StringComparison.Ordinal)
            || pdfSource.Contains("/Subtype /Image", StringComparison.Ordinal);
    }

    private static void AssertXps(string path, string? expectedText, string? forbiddenText)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        if (!HasPackagePart(archive, "[Content_Types].xml"))
        {
            throw new InvalidDataException($"XPS package is missing [Content_Types].xml: {path}");
        }

        ZipArchiveEntry[] fixedPages = [.. archive.Entries
            .Where(static entry =>
                entry.FullName.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase)
                    || entry.FullName.Contains(".fpage/", StringComparison.OrdinalIgnoreCase))];
        if (fixedPages.Length == 0)
        {
            throw new InvalidDataException($"XPS package contains no fixed pages: {path}");
        }

        bool foundExpectedText = false;
        foreach (ZipArchiveEntry entry in fixedPages)
        {
            using Stream stream = entry.Open();
            using StreamReader reader = new(stream, Encoding.UTF8, true);
            string fixedPageXml = reader.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(forbiddenText)
                && fixedPageXml.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("XPS fixed pages contained a forbidden value.");
            }

            if (!string.IsNullOrWhiteSpace(expectedText)
                && fixedPageXml.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            {
                foundExpectedText = true;
            }
        }

        if (string.IsNullOrWhiteSpace(expectedText) || foundExpectedText)
        {
            return;
        }

        throw new InvalidDataException($"XPS fixed pages did not contain '{expectedText}': {path}");
    }

    private static bool HasPackagePart(ZipArchive archive, string partName)
    {
        return archive.GetEntry(partName) is not null
            || archive.Entries.Any(entry => entry.FullName.StartsWith($"{partName}/", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertPostScript(string path, string? expectedText, string? forbiddenText)
    {
        string text = File.ReadAllText(path, Encoding.Latin1);
        if (!text.StartsWith("%!PS", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PostScript output does not start with %!PS: {path}");
        }

        if (!text.Contains("%%Page:", StringComparison.Ordinal)
            && !text.Contains("%%Pages:", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PostScript output does not declare pages: {path}");
        }

        if (!string.IsNullOrWhiteSpace(expectedText)
            && !text.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"PostScript output did not contain '{expectedText}': {path}");
        }

        if (!string.IsNullOrWhiteSpace(forbiddenText)
            && text.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("PostScript output contained a forbidden value.");
        }
    }

    private static void AssertPwgRaster(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length <= 1800)
        {
            throw new InvalidDataException($"PWG Raster output is too small to contain a page: {path}");
        }

        string magic = Encoding.ASCII.GetString(bytes, 0, 4);
        if (magic is not ("RaS2" or "2SaR" or "RaS3" or "3SaR"))
        {
            throw new InvalidDataException($"PWG Raster output has invalid magic '{magic}': {path}");
        }

        int bodyStart = Math.Min(1800, bytes.Length);
        int distinctBodyBytes = bytes
            .Skip(bodyStart)
            .Take(1024 * 1024)
            .Distinct()
            .Take(3)
            .Count();
        if (distinctBodyBytes < 2)
        {
            throw new InvalidDataException($"PWG Raster page body appears blank: {path}");
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{option}'.");
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for '{option}'.");
            }

            options[option[2..]] = args[++index];
        }

        return options;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required --{name} option.");
    }

    private static bool GetOptionalBoolean(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out string? value))
        {
            return false;
        }

        return bool.TryParse(value, out bool result)
            ? result
            : throw new ArgumentException($"Invalid boolean value for --{name}: '{value}'.");
    }
}
