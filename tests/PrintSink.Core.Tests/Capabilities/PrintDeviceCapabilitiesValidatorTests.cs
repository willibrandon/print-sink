using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Core.Tests.Capabilities;

/// <summary>
/// Tests Print Device Capabilities validation.
/// </summary>
[TestClass]
public sealed class PrintDeviceCapabilitiesValidatorTests
{
    /// <summary>
    /// Verifies that a valid minimal PDC passes validation.
    /// </summary>
    [TestMethod]
    public void Validate_accepts_minimal_pdc()
    {
        XDocument document = XDocument.Parse(
            """
            <psf2:PrintDeviceCapabilities xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
                                           xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
              <psk:PageOrientation psf2:psftype="Feature">
                <psk:Portrait psf2:psftype="Option" psf2:default="true" />
              </psk:PageOrientation>
            </psf2:PrintDeviceCapabilities>
            """);

        IReadOnlyList<string> messages = PrintDeviceCapabilitiesValidator.Validate(document);

        Assert.IsEmpty(messages);
    }

    /// <summary>
    /// Verifies that malformed PDC shape returns validation messages.
    /// </summary>
    [TestMethod]
    public void Validate_reports_shape_errors()
    {
        XDocument document = XDocument.Parse(
            """
            <PrintDeviceCapabilities>
              <PageOrientation />
            </PrintDeviceCapabilities>
            """);

        IReadOnlyList<string> messages = PrintDeviceCapabilitiesValidator.Validate(document);

        Assert.Contains("PDC root element must use the Print Schema Framework v2 namespace.", messages);
        Assert.Contains("PDC must contain at least one feature.", messages);
    }

    /// <summary>
    /// Verifies that each feature has at most one default option.
    /// </summary>
    [TestMethod]
    public void Validate_rejects_multiple_default_options()
    {
        XDocument document = XDocument.Parse(
            """
            <psf2:PrintDeviceCapabilities xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
                                           xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
              <psk:PageOrientation psf2:psftype="Feature">
                <psk:Portrait psf2:psftype="Option" psf2:default="true" />
                <psk:Landscape psf2:psftype="Option" psf2:default="true" />
              </psk:PageOrientation>
            </psf2:PrintDeviceCapabilities>
            """);

        IReadOnlyList<string> messages = PrintDeviceCapabilitiesValidator.Validate(document);

        Assert.Contains("Feature 'PageOrientation' must not contain more than one default option.", messages);
    }
}
