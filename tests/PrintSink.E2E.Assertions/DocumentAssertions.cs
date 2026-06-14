using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PrintSink.E2E.Assertions;

internal static partial class DocumentAssertions
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

        switch (format.ToUpperInvariant())
        {
            case "PDF":
                AssertPdf(path, expectedText, forbiddenText, true, requireImage);
                break;
            case "XPS":
            case "OXPS":
                AssertXps(path, expectedText, forbiddenText);
                break;
            case "POSTSCRIPT":
            case "PS":
                AssertPostScript(path, expectedText, forbiddenText);
                break;
            case "PWG":
            case "PWGRASTER":
                AssertPwgRaster(path);
                break;
            case "PCLM":
                AssertPclm(path, forbiddenText);
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
        return ContainsPdfImageDictionary(pdfSource);
    }

    private static bool ContainsPdfImageDictionary(string pdfSource)
    {
        string uncommentedSource = RemovePdfComments(pdfSource);
        foreach (Match dictionary in PdfDictionaryRegex().Matches(uncommentedSource))
        {
            if (PdfImageSubtypeRegex().IsMatch(dictionary.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemovePdfComments(string source)
    {
        StringBuilder uncommentedSource = new(source.Length);
        bool inComment = false;
        foreach (char character in source)
        {
            if (inComment)
            {
                if (character is '\r' or '\n')
                {
                    inComment = false;
                    uncommentedSource.Append(character);
                }

                continue;
            }

            if (character == '%')
            {
                inComment = true;
                continue;
            }

            uncommentedSource.Append(character);
        }

        return uncommentedSource.ToString();
    }

    private static void AssertPclm(string path, string? forbiddenText)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string header = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 64));
        if (!header.StartsWith("%PDF-", StringComparison.Ordinal)
            || !header.Contains("%PCLm 1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PCLm output is missing the PDF/PCLm header markers: {path}");
        }

        AssertPdf(path, null, forbiddenText, false, true);
    }

    private static void AssertXps(string path, string? expectedText, string? forbiddenText)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        XDocument contentTypes = ReadPackagePartXml(archive, "[Content_Types].xml", path);
        if (contentTypes.Root?.Name.LocalName != "Types")
        {
            throw new InvalidDataException($"XPS package has invalid [Content_Types].xml: {path}");
        }

        AssertXpsContentType(contentTypes, "xps-fixeddocumentsequence+xml", path);
        AssertXpsContentType(contentTypes, "xps-fixeddocument+xml", path);
        AssertXpsContentType(contentTypes, "xps-fixedpage+xml", path);

        string[] fixedPageNames = GetPackagePartNames(archive, ".fpage");
        if (fixedPageNames.Length == 0)
        {
            throw new InvalidDataException($"XPS package contains no fixed pages: {path}");
        }

        AssertXpsPackageGraph(archive, path, fixedPageNames);

        bool foundExpectedText = false;
        foreach (string fixedPageName in fixedPageNames)
        {
            string fixedPageXml = ReadPackagePartText(archive, fixedPageName);
            XDocument fixedPage = ParsePackagePartXml(fixedPageXml, fixedPageName, path);
            if (fixedPage.Root?.Name.LocalName != "FixedPage")
            {
                throw new InvalidDataException($"XPS fixed page has invalid root element '{fixedPage.Root?.Name.LocalName}': {path}");
            }

            if (!string.IsNullOrWhiteSpace(forbiddenText)
                && fixedPageXml.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("XPS fixed pages contained a forbidden value.");
            }

            if (!string.IsNullOrWhiteSpace(expectedText)
                && FixedPageContainsText(fixedPage, expectedText))
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

    private static void AssertXpsPackageGraph(ZipArchive archive, string path, string[] fixedPageNames)
    {
        XDocument packageRelationships = ReadPackagePartXml(archive, "_rels/.rels", path);
        if (packageRelationships.Root?.Name.LocalName != "Relationships")
        {
            throw new InvalidDataException($"XPS package has invalid package relationships: {path}");
        }

        XElement? fixedRepresentation = packageRelationships
            .Root
            .Elements()
            .FirstOrDefault(static element =>
                element.Name.LocalName == "Relationship"
                && element.Attribute("Type")?.Value.Contains("fixedrepresentation", StringComparison.OrdinalIgnoreCase) == true
                && !string.Equals(
                    element.Attribute("TargetMode")?.Value,
                    "External",
                    StringComparison.OrdinalIgnoreCase));
        if (fixedRepresentation is null)
        {
            throw new InvalidDataException($"XPS package is missing a fixed representation relationship: {path}");
        }

        string? sequenceTarget = (string?)fixedRepresentation.Attribute("Target");
        if (string.IsNullOrWhiteSpace(sequenceTarget))
        {
            throw new InvalidDataException($"XPS fixed representation relationship has no target: {path}");
        }

        string sequencePartName = ResolvePackagePartName(string.Empty, sequenceTarget);
        if (!PackagePartExists(archive, sequencePartName))
        {
            throw new InvalidDataException($"XPS fixed document sequence is missing: {sequencePartName}");
        }

        XDocument fixedDocumentSequence = ReadPackagePartXml(archive, sequencePartName, path);
        if (fixedDocumentSequence.Root?.Name.LocalName != "FixedDocumentSequence")
        {
            throw new InvalidDataException($"XPS fixed document sequence has invalid root element '{fixedDocumentSequence.Root?.Name.LocalName}': {path}");
        }

        string[] documentPartNames = [.. fixedDocumentSequence
            .Descendants()
            .Where(static element => element.Name.LocalName == "DocumentReference")
            .Select(element => (string?)element.Attribute("Source"))
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Select(source => ResolvePackagePartName(sequencePartName, source!))];
        if (documentPartNames.Length == 0)
        {
            throw new InvalidDataException($"XPS fixed document sequence references no fixed documents: {path}");
        }

        bool referencesFixedPage = false;
        foreach (string documentPartName in documentPartNames)
        {
            if (!PackagePartExists(archive, documentPartName))
            {
                throw new InvalidDataException($"XPS fixed document is missing: {documentPartName}");
            }

            XDocument fixedDocument = ReadPackagePartXml(archive, documentPartName, path);
            if (fixedDocument.Root?.Name.LocalName != "FixedDocument")
            {
                throw new InvalidDataException($"XPS fixed document has invalid root element '{fixedDocument.Root?.Name.LocalName}': {path}");
            }

            string[] referencedPageNames = [.. fixedDocument
                .Descendants()
                .Where(static element => element.Name.LocalName == "PageContent")
                .Select(element => (string?)element.Attribute("Source"))
                .Where(static source => !string.IsNullOrWhiteSpace(source))
                .Select(source => ResolvePackagePartName(documentPartName, source!))];
            if (referencedPageNames.Length == 0)
            {
                throw new InvalidDataException($"XPS fixed document references no fixed pages: {path}");
            }

            foreach (string referencedPageName in referencedPageNames)
            {
                if (!fixedPageNames.Contains(referencedPageName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"XPS fixed document references missing fixed page: {referencedPageName}");
                }

                referencesFixedPage = true;
            }
        }

        if (!referencesFixedPage)
        {
            throw new InvalidDataException($"XPS package graph does not reach a fixed page: {path}");
        }
    }

    private static bool FixedPageContainsText(XDocument fixedPage, string expectedText)
    {
        StringBuilder glyphText = new();
        foreach (XElement glyphs in fixedPage
            .Descendants()
            .Where(static element => element.Name.LocalName == "Glyphs"))
        {
            string? unicodeString = (string?)glyphs.Attribute("UnicodeString");
            if (!string.IsNullOrWhiteSpace(unicodeString))
            {
                glyphText.Append(unicodeString);
            }
        }

        return glyphText.ToString().Contains(expectedText, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertXpsContentType(XDocument contentTypes, string requiredToken, string path)
    {
        bool hasContentType = contentTypes
            .Descendants()
            .Any(element =>
                element.Attribute("ContentType")?.Value.Contains(requiredToken, StringComparison.OrdinalIgnoreCase) == true);
        if (!hasContentType)
        {
            throw new InvalidDataException($"XPS package does not declare a {requiredToken} content type: {path}");
        }
    }

    private static string[] GetPackagePartNames(ZipArchive archive, string extension)
    {
        SortedSet<string> partNames = new(StringComparer.OrdinalIgnoreCase);
        string interleavedMarker = $"{extension}/";
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                partNames.Add(entry.FullName);
                continue;
            }

            int markerIndex = entry.FullName.IndexOf(interleavedMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                partNames.Add(entry.FullName[..(markerIndex + extension.Length)]);
            }
        }

        return [.. partNames];
    }

    private static XDocument ReadPackagePartXml(ZipArchive archive, string partName, string path)
    {
        string xml = ReadPackagePartText(archive, partName);
        return ParsePackagePartXml(xml, partName, path);
    }

    private static XDocument ParsePackagePartXml(string xml, string partName, string path)
    {
        try
        {
            return XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidDataException($"XPS package part has invalid XML '{partName}': {path}", ex);
        }
    }

    private static string ReadPackagePartText(ZipArchive archive, string partName)
    {
        ZipArchiveEntry? directEntry = archive.GetEntry(partName);
        if (directEntry is not null)
        {
            using Stream directStream = directEntry.Open();
            using StreamReader directReader = new(directStream, Encoding.UTF8, true);
            return directReader.ReadToEnd();
        }

        ZipArchiveEntry[] pieceEntries = [.. archive.Entries
            .Where(entry => entry.FullName.StartsWith($"{partName}/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static entry => GetInterleavedPieceIndex(entry))];
        if (pieceEntries.Length == 0)
        {
            throw new InvalidDataException($"XPS package part is missing: {partName}");
        }

        using MemoryStream buffer = new();
        foreach (ZipArchiveEntry pieceEntry in pieceEntries)
        {
            using Stream pieceStream = pieceEntry.Open();
            pieceStream.CopyTo(buffer);
        }

        buffer.Position = 0;
        using StreamReader reader = new(buffer, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static bool PackagePartExists(ZipArchive archive, string partName)
    {
        return archive.GetEntry(partName) is not null
            || archive.Entries.Any(entry => entry.FullName.StartsWith($"{partName}/", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolvePackagePartName(string sourcePartName, string target)
    {
        string targetPath = Uri.UnescapeDataString(target.Split('#', 2)[0])
            .Replace('\\', '/')
            .Trim();
        if (targetPath.StartsWith('/'))
        {
            return NormalizePackagePartName(targetPath);
        }

        string sourcePath = NormalizePackagePartName(sourcePartName);
        int slashIndex = sourcePath.LastIndexOf('/');
        string sourceDirectory = slashIndex >= 0
            ? sourcePath[..(slashIndex + 1)]
            : string.Empty;
        return NormalizePackagePartName(sourceDirectory + targetPath);
    }

    private static string NormalizePackagePartName(string partName)
    {
        List<string> segments = [];
        foreach (string segment in partName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException($"XPS package part path escapes the package root: {partName}");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static int GetInterleavedPieceIndex(ZipArchiveEntry entry)
    {
        string name = entry.FullName[(entry.FullName.LastIndexOf('/') + 1)..];
        int start = name.IndexOf('[', StringComparison.Ordinal);
        int end = name.IndexOf(']', StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            throw new InvalidDataException($"XPS interleaved package piece has invalid name: {entry.FullName}");
        }

        return int.Parse(name[(start + 1)..end], System.Globalization.CultureInfo.InvariantCulture);
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

        if (!HasResolvedPostScriptPageCount(text))
        {
            throw new InvalidDataException($"PostScript output does not contain a resolved page count: {path}");
        }

        if (!HasResolvedPostScriptBoundingBox(text))
        {
            throw new InvalidDataException($"PostScript output does not contain a resolved bounding box: {path}");
        }

        if (!text.Contains("%%PageTrailer", StringComparison.Ordinal)
            || !text.Contains("%%Trailer", StringComparison.Ordinal)
            || !text.Contains("%%EOF", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PostScript output is missing required DSC closing markers: {path}");
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

    private static bool HasResolvedPostScriptPageCount(string text)
    {
        using StringReader reader = new(text);
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith("%%Pages:", StringComparison.Ordinal))
            {
                continue;
            }

            string value = line["%%Pages:".Length..].Trim();
            if (value.Contains("(atend)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int pageCount)
                && pageCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasResolvedPostScriptBoundingBox(string text)
    {
        using StringReader reader = new(text);
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith("%%BoundingBox:", StringComparison.Ordinal))
            {
                continue;
            }

            string value = line["%%BoundingBox:".Length..].Trim();
            if (value.Contains("(atend)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                continue;
            }

            double[] coordinates = new double[parts.Length];
            bool parsed = true;
            for (int index = 0; index < parts.Length; index++)
            {
                if (double.TryParse(
                    parts[index],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double coordinate)
                    && double.IsFinite(coordinate))
                {
                    coordinates[index] = coordinate;
                    continue;
                }

                parsed = false;
                break;
            }

            if (parsed && coordinates[2] > coordinates[0] && coordinates[3] > coordinates[1])
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertPwgRaster(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        const int syncWordLength = 4;
        const int version2HeaderLength = 1796;
        if (bytes.Length <= syncWordLength + version2HeaderLength)
        {
            throw new InvalidDataException($"PWG Raster output is too small to contain a page: {path}");
        }

        string magic = Encoding.ASCII.GetString(bytes, 0, 4);
        bool isBigEndian = magic is "RaS2" or "RaS3";
        bool isCompressed = magic is "RaS2" or "2SaR";
        if (!isBigEndian && magic is not ("2SaR" or "3SaR"))
        {
            throw new InvalidDataException($"PWG Raster output has invalid magic '{magic}': {path}");
        }

        uint width = ReadRasterUInt32(bytes, syncWordLength + 372, isBigEndian);
        uint height = ReadRasterUInt32(bytes, syncWordLength + 376, isBigEndian);
        uint bitsPerColor = ReadRasterUInt32(bytes, syncWordLength + 384, isBigEndian);
        uint bitsPerPixel = ReadRasterUInt32(bytes, syncWordLength + 388, isBigEndian);
        uint bytesPerLine = ReadRasterUInt32(bytes, syncWordLength + 392, isBigEndian);
        uint colorOrder = ReadRasterUInt32(bytes, syncWordLength + 396, isBigEndian);
        uint colorSpace = ReadRasterUInt32(bytes, syncWordLength + 400, isBigEndian);
        uint numberOfColors = ReadRasterUInt32(bytes, syncWordLength + 420, isBigEndian);

        if (width == 0 || height == 0)
        {
            throw new InvalidDataException($"PWG Raster page dimensions are invalid: {path}");
        }

        if (bitsPerColor is not (1 or 2 or 4 or 8 or 16)
            || bitsPerPixel == 0
            || bitsPerPixel > 240)
        {
            throw new InvalidDataException($"PWG Raster bit depth is invalid: {path}");
        }

        if (bytesPerLine == 0 || bytesPerLine < (width * bitsPerPixel + 7) / 8)
        {
            throw new InvalidDataException($"PWG Raster stride is invalid: {path}");
        }

        if (colorOrder > 2)
        {
            throw new InvalidDataException($"PWG Raster color order is invalid: {path}");
        }

        if (colorSpace > 62 || numberOfColors is < 1 or > 15)
        {
            throw new InvalidDataException($"PWG Raster color metadata is invalid: {path}");
        }

        int bodyStart = syncWordLength + version2HeaderLength;
        ulong declaredBodyBytes = (ulong)bytesPerLine * height;
        ulong actualBodyBytes = (ulong)(bytes.Length - bodyStart);
        if (!isCompressed && actualBodyBytes < declaredBodyBytes)
        {
            throw new InvalidDataException($"PWG Raster page body is shorter than declared page data: {path}");
        }

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

    private static uint ReadRasterUInt32(byte[] bytes, int offset, bool isBigEndian)
    {
        ReadOnlySpan<byte> value = bytes.AsSpan(offset, 4);
        return isBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(value)
            : BinaryPrimitives.ReadUInt32LittleEndian(value);
    }

    [GeneratedRegex("<<[\\s\\S]*?>>", RegexOptions.CultureInvariant)]
    private static partial Regex PdfDictionaryRegex();

    [GeneratedRegex(@"/Subtype\s*/Image\b", RegexOptions.CultureInvariant)]
    private static partial Regex PdfImageSubtypeRegex();

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

    private static string GetRequired(Dictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required --{name} option.");
    }

    private static bool GetOptionalBoolean(Dictionary<string, string> options, string name)
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
