using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;

namespace PrintSink.Core.Tests.Pdl;

/// <summary>
/// Tests PDL routing decisions.
/// </summary>
[TestClass]
public sealed class PdlRouterTests
{
    private readonly PdlRouter router = new();

    /// <summary>
    /// Verifies the routing matrix for built-in endpoints.
    /// </summary>
    [TestMethod]
    [DataRow("application/oxps", EndpointKind.Pdf, PdlActionKind.Convert, PdlConversionKind.XpsToPdf)]
    [DataRow("application/vnd.ms-xpsdocument", EndpointKind.Pdf, PdlActionKind.Convert, PdlConversionKind.XpsToPdf)]
    [DataRow("application/oxps", EndpointKind.Xps, PdlActionKind.Copy, null)]
    [DataRow("application/pdf", EndpointKind.Pdf, PdlActionKind.Copy, null)]
    [DataRow("application/pdf", EndpointKind.Cloud, PdlActionKind.Copy, null)]
    [DataRow("application/postscript", EndpointKind.PostScript, PdlActionKind.Copy, null)]
    [DataRow("application/oxps", EndpointKind.PwgRaster, PdlActionKind.Convert, PdlConversionKind.XpsToPwgRaster)]
    [DataRow("application/oxps", EndpointKind.Pclm, PdlActionKind.Convert, PdlConversionKind.XpsToPclm)]
    [DataRow("application/pdf", EndpointKind.Xps, PdlActionKind.Reject, null)]
    [DataRow("application/octet-stream", EndpointKind.Pdf, PdlActionKind.Reject, null)]
    public void Resolve_returns_expected_plan(
        string contentType,
        EndpointKind endpointKind,
        PdlActionKind expectedAction,
        PdlConversionKind? expectedConversion)
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);

        PdlPlan plan = router.Resolve(contentType, endpoint);

        Assert.AreEqual(expectedAction, plan.ActionKind);
        Assert.AreEqual(endpoint.TargetFormat, plan.TargetFormat);
        Assert.AreEqual(expectedConversion, plan.ConversionKind);
    }

    /// <summary>
    /// Verifies that unknown content types preserve a null source format.
    /// </summary>
    [TestMethod]
    public void Resolve_rejects_unknown_content_type_without_source_format()
    {
        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(EndpointKind.Pdf);

        PdlPlan plan = router.Resolve("application/octet-stream", endpoint);

        Assert.AreEqual(PdlActionKind.Reject, plan.ActionKind);
        Assert.IsNull(plan.SourceFormat);
    }
}
