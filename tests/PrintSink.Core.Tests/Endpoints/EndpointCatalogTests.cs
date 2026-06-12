using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Endpoints;

/// <summary>
/// Tests the built-in endpoint catalog.
/// </summary>
[TestClass]
public sealed class EndpointCatalogTests
{
    /// <summary>
    /// Verifies the endpoint display order.
    /// </summary>
    [TestMethod]
    public void All_returns_expected_endpoints_in_display_order()
    {
        EndpointKind[] expected =
        [
            EndpointKind.Pdf,
            EndpointKind.Xps,
            EndpointKind.PostScript,
            EndpointKind.Cloud,
            EndpointKind.PwgRaster,
        ];

        CollectionAssert.AreEqual(expected, EndpointCatalog.All.Select(endpoint => endpoint.Kind).ToArray());
    }

    /// <summary>
    /// Verifies that printer URIs uniquely identify endpoints.
    /// </summary>
    [TestMethod]
    public void All_has_unique_printer_uris()
    {
        Uri[] printerUris = [.. EndpointCatalog.All.Select(endpoint => endpoint.PrinterUri)];
        int distinctCount = printerUris.Select(uri => uri.AbsoluteUri).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Assert.AreEqual(printerUris.Length, distinctCount);
    }

    /// <summary>
    /// Verifies that the cloud endpoint is not file-backed.
    /// </summary>
    [TestMethod]
    public void Cloud_endpoint_does_not_require_target_file()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Cloud);

        Assert.IsFalse(endpoint.RequiresTargetFile);
        Assert.IsNull(endpoint.DefaultExtension);
        Assert.AreEqual(PdlFormat.Pdf, endpoint.TargetFormat);
    }

    /// <summary>
    /// Verifies that file endpoints declare file output metadata.
    /// </summary>
    [TestMethod]
    public void File_endpoints_require_target_file_and_extension()
    {
        foreach (VirtualEndpoint endpoint in EndpointCatalog.All.Where(endpoint => endpoint.Kind != EndpointKind.Cloud))
        {
            Assert.IsTrue(endpoint.RequiresTargetFile);
            Assert.IsFalse(string.IsNullOrWhiteSpace(endpoint.DefaultExtension));
            Assert.IsNotEmpty(endpoint.OutputExtensions);
        }
    }

    /// <summary>
    /// Verifies the XPS endpoint exposes both classic XPS and OpenXPS file extensions.
    /// </summary>
    [TestMethod]
    public void Xps_endpoint_declares_xps_and_oxps_output_extensions()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Xps);

        CollectionAssert.AreEquivalent(
            new[] { ".xps", ".oxps" },
            endpoint.OutputExtensions.ToArray());
    }

    /// <summary>
    /// Verifies URI resolution for registered endpoint addresses.
    /// </summary>
    [TestMethod]
    public void TryResolve_accepts_registered_uri_case_insensitively()
    {
        Uri printerUri = new("PRINTSINK:PRINT-TO-PDF");

        bool resolved = EndpointCatalog.TryResolve(printerUri, out VirtualEndpoint? endpoint);

        Assert.IsTrue(resolved);
        Assert.IsNotNull(endpoint);
        Assert.AreEqual(EndpointKind.Pdf, endpoint.Kind);
    }
}
