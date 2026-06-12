using System.Text;
using PrintSink.Endpoints;
using PrintSink.Pdl;

namespace PrintSink.Core.Tests.Endpoints;

/// <summary>
/// Tests for concrete sink implementations.
/// </summary>
[TestClass]
public sealed class SinkTests
{
    /// <summary>
    /// Verifies <see cref="FileSink"/> copies source bytes to the target stream.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task FileSink_CopiesPdlToTargetStream()
    {
        await using MemoryStream source = new(Encoding.UTF8.GetBytes("%PDF-1.7"));
        await using MemoryStream target = new();
        FileSink sink = new(target, leaveOpen: true);
        SinkWriteContext context = new(EndpointCatalog.Pdf, PdlFormatInfo.PdfContentType, "test");

        await sink.WriteAsync(source, context);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("%PDF-1.7"), target.ToArray());
    }

    /// <summary>
    /// Verifies <see cref="CloudSink"/> delegates upload to the configured client.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestMethod]
    public async Task CloudSink_DelegatesToUploadClient()
    {
        RecordingCloudUploadClient client = new();
        CloudSink sink = new(client);
        await using MemoryStream source = new(Encoding.UTF8.GetBytes("cloud"));
        SinkWriteContext context = new(EndpointCatalog.Cloud, PdlFormatInfo.PdfContentType, "cloud-job");

        await sink.WriteAsync(source, context);

        Assert.AreEqual("cloud-job", client.JobName);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("cloud"), client.Bytes);
    }
}
