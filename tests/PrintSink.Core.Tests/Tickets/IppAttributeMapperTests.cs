using System.Xml.Linq;
using PrintSink.Core.Tickets;

namespace PrintSink.Core.Tests.Tickets;

/// <summary>
/// Tests print-ticket to IPP attribute mapping.
/// </summary>
[TestClass]
public sealed class IppAttributeMapperTests
{
    private readonly IppAttributeMapper mapper = new();

    /// <summary>
    /// Verifies that common print-ticket features map to IPP job attributes.
    /// </summary>
    [TestMethod]
    public void FromPrintTicket_maps_common_features()
    {
        XDocument printTicket = CreatePrintTicket();

        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(printTicket);

        Assert.AreEqual("northamericaletter", attributes["media"].Values[0]);
        Assert.AreEqual("two-sided-long-edge", attributes["sides"].Values[0]);
        Assert.AreEqual("monochrome", attributes["print-color-mode"].Values[0]);
        Assert.AreEqual("4", attributes["orientation-requested"].Values[0]);
        Assert.AreEqual("5", attributes["print-quality"].Values[0]);
    }

    /// <summary>
    /// Verifies that common print-ticket parameters map to IPP job attributes.
    /// </summary>
    [TestMethod]
    public void FromPrintTicket_maps_parameters()
    {
        XDocument printTicket = CreatePrintTicket();

        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(printTicket);

        Assert.AreEqual("3", attributes["copies"].Values[0]);
        Assert.AreEqual("2", attributes["number-up"].Values[0]);
    }

    /// <summary>
    /// Verifies that merge policy removals return a filtered attribute map.
    /// </summary>
    [TestMethod]
    public void ApplyMergePolicy_removes_configured_attributes()
    {
        XDocument printTicket = CreatePrintTicket();
        IReadOnlyDictionary<string, IppAttributeValue> attributes = mapper.FromPrintTicket(printTicket);
        AttributeMergePolicyOptions options = new(["media", "copies"]);

        IReadOnlyDictionary<string, IppAttributeValue> result = mapper.ApplyMergePolicy(attributes, options);

        Assert.IsFalse(result.ContainsKey("media"));
        Assert.IsFalse(result.ContainsKey("copies"));
        Assert.IsTrue(result.ContainsKey("sides"));
    }

    private static XDocument CreatePrintTicket()
    {
        return XDocument.Parse(
            """
            <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                             xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                             xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <psf:Feature name="psk:PageMediaSize">
                <psf:Option name="psk:NorthAmericaLetter" />
              </psf:Feature>
              <psf:Feature name="psk:JobDuplexAllDocumentsContiguously">
                <psf:Option name="psk:TwoSidedLongEdge" />
              </psf:Feature>
              <psf:Feature name="psk:PageOutputColor">
                <psf:Option name="psk:Monochrome" />
              </psf:Feature>
              <psf:Feature name="psk:PageOrientation">
                <psf:Option name="psk:Landscape" />
              </psf:Feature>
              <psf:Feature name="psk:PageOutputQuality">
                <psf:Option name="psk:High" />
              </psf:Feature>
              <psf:ParameterInit name="psk:JobCopiesAllDocuments">
                <psf:Value xsi:type="xsd:integer">3</psf:Value>
              </psf:ParameterInit>
              <psf:ParameterInit name="psk:JobNUpAllDocumentsContiguously">
                <psf:Value xsi:type="xsd:integer">2</psf:Value>
              </psf:ParameterInit>
            </psf:PrintTicket>
            """);
    }
}
