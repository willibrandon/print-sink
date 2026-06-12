using PrintSink.Endpoints;
using PrintSink.Pdl;

namespace PrintSink.Core.Tests.Endpoints;

/// <summary>
/// Tests for <see cref="EndpointCatalog"/>.
/// </summary>
[TestClass]
internal sealed class EndpointCatalogTests
{
    /// <summary>
    /// Verifies the design-approved manifest queue count.
    /// </summary>
    [TestMethod]
    public void BuiltInQueuesReturnsFiveManifestQueues()
    {
        Assert.AreEqual(5, EndpointCatalog.BuiltInQueues.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                EndpointKind.PdfFile,
                EndpointKind.XpsFile,
                EndpointKind.PostScriptFile,
                EndpointKind.Cloud,
                EndpointKind.PwgRasterFile,
            },
            EndpointCatalog.BuiltInQueues.Select(endpoint => endpoint.Kind).ToArray());
    }

    /// <summary>
    /// Verifies the cloud endpoint does not use the Save As broker.
    /// </summary>
    [TestMethod]
    public void CloudEndpointIsNotFileBacked()
    {
        Assert.IsFalse(EndpointCatalog.Cloud.UsesSaveAsDialog);
        Assert.IsFalse(EndpointCatalog.Cloud.IsFileBacked);
        Assert.AreEqual(PdlFormat.Pdf, EndpointCatalog.Cloud.TargetFormat);
    }

    /// <summary>
    /// Verifies URI path lookup accepts paths without a leading slash.
    /// </summary>
    [TestMethod]
    public void FromEndpointPathWithoutLeadingSlashReturnsEndpoint()
    {
        VirtualEndpoint endpoint = EndpointCatalog.FromEndpointPath("pdf");

        Assert.AreSame(EndpointCatalog.Pdf, endpoint);
    }
}
