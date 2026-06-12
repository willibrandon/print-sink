using PrintSink.Tickets;

namespace PrintSink.Core.Tests.Tickets;

/// <summary>
/// Tests for <see cref="IppAttributeMapper"/>.
/// </summary>
[TestClass]
public sealed class IppAttributeMapperTests
{
    private const string TicketXml = """
        <psf:PrintTicket
            xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
            xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
          <psf:Feature name="psk:PageOutputColor">
            <psf:Option name="psk:Monochrome" />
          </psf:Feature>
          <psf:Feature name="psk:JobDuplexAllDocumentsContiguously">
            <psf:Option name="psk:TwoSidedLongEdge" />
          </psf:Feature>
          <psf:Feature name="psk:PageOrientation">
            <psf:Option name="psk:Landscape" />
          </psf:Feature>
          <psf:Feature name="psk:PageMediaSize">
            <psf:Option name="psk:ISOA4" />
          </psf:Feature>
          <psf:ParameterInit name="psk:JobCopiesAllDocuments">
            <psf:Value>2</psf:Value>
          </psf:ParameterInit>
        </psf:PrintTicket>
        """;

    /// <summary>
    /// Verifies common print ticket values map to IPP attributes.
    /// </summary>
    [TestMethod]
    public void FromPrintTicket_DefaultOptions_MapsExpectedAttributes()
    {
        IppAttributeMapper mapper = new();

        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(TicketXml, AttributeMergePolicyOptions.Default);

        Assert.AreEqual("monochrome", attributes["print-color-mode"].StringValues[0]);
        Assert.AreEqual("two-sided-long-edge", attributes["sides"].StringValues[0]);
        Assert.AreEqual("4", attributes["orientation-requested"].StringValues[0]);
        Assert.AreEqual("2", attributes["copies"].StringValues[0]);
        Assert.IsFalse(attributes.ContainsKey("media"));
    }

    /// <summary>
    /// Verifies media size is preserved when requested by merge options.
    /// </summary>
    [TestMethod]
    public void FromPrintTicket_WhenMediaRemovalDisabled_IncludesMedia()
    {
        IppAttributeMapper mapper = new();
        AttributeMergePolicyOptions options = new(IppAttributeMergePolicy.Replace, removeMediaSize: false, includeCopies: true);

        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(TicketXml, options);

        Assert.AreEqual("isoa4", attributes["media"].StringValues[0]);
    }

    /// <summary>
    /// Verifies encrypted job password attributes are added in the expected operation collection shape.
    /// </summary>
    [TestMethod]
    public void FromPrintTicket_WithPasswordOptions_AddsOperationAttributes()
    {
        IppAttributeMapper mapper = new();
        JobPasswordOptions password = new(new byte[] { 1, 2, 3, 4 }, "printsink-aes256-gcm");

        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(TicketXml, AttributeMergePolicyOptions.Default, password);

        Assert.IsTrue(attributes["job-password"].HasBinaryValue);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, attributes["job-password"].GetBinaryValue());
        Assert.AreEqual("printsink-aes256-gcm", attributes["job-password-encryption"].StringValues[0]);
        CollectionAssert.Contains(attributes["msft-operation-attribute-col"].StringValues.ToArray(), "job-password");
        CollectionAssert.Contains(attributes["msft-operation-attribute-col"].StringValues.ToArray(), "job-password-encryption");
    }
}
