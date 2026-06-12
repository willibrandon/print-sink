namespace PrintSink.Core.Tests.Pdl;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PrintSink.Endpoints;
using PrintSink.Pdl;

[TestClass]
public sealed class PdlRouterTests
{
  private readonly PdlRouter router = new();

  public TestContext TestContext { get; set; } = null!;

  [DataRow("/pdf", PdlFormat.Pdf, PdlActionKind.Copy, null)]
  [DataRow("/pdf", PdlFormat.Oxps, PdlActionKind.Convert, PdlConversionKind.XpsToPdf)]
  [DataRow("/xps", PdlFormat.Oxps, PdlActionKind.Copy, null)]
  [DataRow("/postscript", PdlFormat.PostScript, PdlActionKind.Copy, null)]
  [DataRow("/pwg", PdlFormat.Oxps, PdlActionKind.Convert, PdlConversionKind.XpsToPwgRaster)]
  [TestMethod]
  public void ResolveReturnsExpectedPlan(
    string endpointPath,
    PdlFormat sourceFormat,
    PdlActionKind expectedAction,
    PdlConversionKind? expectedConversion)
  {
    Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

    VirtualEndpoint endpoint = EndpointCatalog.GetByPath(endpointPath);

    PdlPlan plan = router.Resolve(PdlFormatInfo.GetContentType(sourceFormat), endpoint);

    Assert.AreEqual(expectedAction, plan.Action);
    Assert.AreEqual(sourceFormat, plan.SourceFormat);
    Assert.AreEqual(expectedConversion, plan.ConversionKind);
  }

  [TestMethod]
  public void ResolveRejectsUnknownContentType()
  {
    Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

    PdlPlan plan = router.Resolve("application/not-real", EndpointCatalog.GetByPath("/pdf"));

    Assert.AreEqual(PdlActionKind.Reject, plan.Action);
    Assert.IsNull(plan.SourceFormat);
  }
}
