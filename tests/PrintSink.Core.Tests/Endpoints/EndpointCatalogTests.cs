namespace PrintSink.Core.Tests.Endpoints;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PrintSink.Endpoints;

[TestClass]
public sealed class EndpointCatalogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BuiltInQueuesHaveUniquePaths()
    {
        Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

        string[] paths = [.. EndpointCatalog.BuiltInQueues.Select(endpoint => endpoint.EndpointPath)];

        CollectionAssert.AllItemsAreUnique(paths);
    }

    [TestMethod]
    public void CloudEndpointDoesNotUseSaveAs()
    {
        Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

        VirtualEndpoint cloud = EndpointCatalog.GetByPath("/cloud");

        Assert.IsFalse(cloud.UsesSaveAsDialog);
        Assert.AreEqual(0, cloud.OutputFileExtensions.Count);
    }

    [TestMethod]
    public void FileEndpointsDeclareExtensions()
    {
        Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

        foreach (VirtualEndpoint endpoint in EndpointCatalog.BuiltInQueues.Where(endpoint => endpoint.UsesSaveAsDialog))
        {
            Assert.IsTrue(endpoint.OutputFileExtensions.Count > 0, endpoint.DisplayName);
            Assert.IsTrue(endpoint.OutputFileExtensions.All(extension => extension.StartsWith('.')), endpoint.DisplayName);
        }
    }
}
