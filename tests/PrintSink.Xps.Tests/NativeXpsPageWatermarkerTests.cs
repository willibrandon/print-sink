using System.IO.Compression;
using System.Text;
using PrintSink.Core.Watermark;
using PrintSink.Xps.Projections;

namespace PrintSink.Xps.Tests;

/// <summary>
/// Tests the native XPS watermarker through its C# projection.
/// </summary>
[TestClass]
[DoNotParallelize]
internal sealed class NativeXpsPageWatermarkerTests
{
    private const string WatermarkText = "PrintSink Test";

    /// <summary>
    /// Verifies that text watermarking produces a readable XPS package.
    /// </summary>
    [TestMethod]
    public async Task ApplyAsyncWritesTextWatermarkedXpsPackage()
    {
        NativeXpsPageWatermarker watermarker = new();
        watermarker.ApplyText(new TextWatermark(WatermarkText, "Segoe UI", 48, 0.35, -30, 0, 0));

        using MemoryStream source = new(CreateMinimalXpsPackage());
        using Stream output = await watermarker.ApplyAsync(source, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.IsGreaterThan(0L, output.Length);
        using ZipArchive archive = new(output, ZipArchiveMode.Read);
        Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.EndsWith(".odttf", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(ReadFixedPageXml(archive).Any(page => page.Contains(WatermarkText, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies that image watermarking embeds an image resource in the generated XPS package.
    /// </summary>
    [TestMethod]
    public async Task ApplyAsyncWritesImageWatermarkedXpsPackage()
    {
        string imagePath = Path.Combine(TestContext.TestRunResultsDirectory!, "printsink-watermark.png");
        await File.WriteAllBytesAsync(imagePath, CreatePngBytes(), TestContext.CancellationToken).ConfigureAwait(false);

        NativeXpsPageWatermarker watermarker = new();
        watermarker.ApplyImage(new ImageWatermark(imagePath, 48, 48, 0.5, 10, 0, 0));

        using MemoryStream source = new(CreateMinimalXpsPackage());
        using Stream output = await watermarker.ApplyAsync(source, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.IsGreaterThan(0L, output.Length);
        using ZipArchive archive = new(output, ZipArchiveMode.Read);
        Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.Contains("PrintSinkWatermarkImage", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(ReadFixedPageXml(archive).Any(page => page.Contains("PrintSinkWatermarkImage", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Verifies that malformed XPS input reports a native package failure.
    /// </summary>
    [TestMethod]
    public async Task ApplyAsyncThrowsForCorruptXpsPackage()
    {
        NativeXpsPageWatermarker watermarker = new();
        watermarker.ApplyText(new TextWatermark(WatermarkText, "Segoe UI", 48, 0.35, -30, 0, 0));

        using MemoryStream source = new(Encoding.UTF8.GetBytes("not an xps package"));

        try
        {
            await watermarker.ApplyAsync(source, TestContext.CancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        Assert.Fail("Expected malformed XPS input to fail.");
    }

    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    private static byte[] CreateMinimalXpsPackage()
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="fdseq" ContentType="application/vnd.ms-package.xps-fixeddocumentsequence+xml" />
                  <Default Extension="fdoc" ContentType="application/vnd.ms-package.xps-fixeddocument+xml" />
                  <Default Extension="fpage" ContentType="application/vnd.ms-package.xps-fixedpage+xml" />
                </Types>
                """);
            WriteEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="R1" Type="http://schemas.microsoft.com/xps/2005/06/fixedrepresentation" Target="/Documents/1/FixedDocSeq.fdseq" />
                </Relationships>
                """);
            WriteEntry(archive, "Documents/1/FixedDocSeq.fdseq", """
                <?xml version="1.0" encoding="utf-8"?>
                <FixedDocumentSequence xmlns="http://schemas.microsoft.com/xps/2005/06">
                  <DocumentReference Source="/Documents/1/FixedDoc.fdoc" />
                </FixedDocumentSequence>
                """);
            WriteEntry(archive, "Documents/1/FixedDoc.fdoc", """
                <?xml version="1.0" encoding="utf-8"?>
                <FixedDocument xmlns="http://schemas.microsoft.com/xps/2005/06">
                  <PageContent Source="/Documents/1/Pages/1.fpage" />
                </FixedDocument>
                """);
            WriteEntry(archive, "Documents/1/Pages/1.fpage", """
                <?xml version="1.0" encoding="utf-8"?>
                <FixedPage xmlns="http://schemas.microsoft.com/xps/2005/06" Width="816" Height="1056" xml:lang="en-US">
                  <Path Data="M 96,96 L 720,96 L 720,960 L 96,960 Z" Fill="#FFFFFFFF" />
                </FixedPage>
                """);
        }

        return stream.ToArray();
    }

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lX8xjAAAAABJRU5ErkJggg==");
    }

    private static string[] ReadFixedPageXml(ZipArchive archive)
    {
        string[] fixedPageNames = GetPackagePartNames(archive, ".fpage");
        Assert.IsNotEmpty(fixedPageNames);

        return [.. fixedPageNames.Select(partName => ReadPackagePartText(archive, partName))];
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

        return Encoding.UTF8.GetString(buffer.ToArray());
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

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes);
    }
}
