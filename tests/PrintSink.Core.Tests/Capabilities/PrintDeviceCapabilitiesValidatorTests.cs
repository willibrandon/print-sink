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

    /// <summary>
    /// Verifies that non-job root custom features are rejected before package provisioning.
    /// </summary>
    [TestMethod]
    public void Validate_rejects_non_job_root_custom_features()
    {
        XDocument document = XDocument.Parse(
            """
            <psf2:PrintDeviceCapabilities xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
                                           xmlns:printsink="https://schemas.printsink.dev/printing/keywords">
              <printsink:WatermarkMode psf2:psftype="Feature">
                <printsink:Off psf2:psftype="Option" psf2:default="true" />
              </printsink:WatermarkMode>
            </psf2:PrintDeviceCapabilities>
            """);

        IReadOnlyList<string> messages = PrintDeviceCapabilitiesValidator.Validate(document);

        Assert.Contains("Custom root feature 'WatermarkMode' must be job-scoped.", messages);
    }

    /// <summary>
    /// Verifies that media-size option properties use the order accepted by Windows provisioning.
    /// </summary>
    [TestMethod]
    public void Validate_rejects_media_size_properties_in_unsupported_order()
    {
        XDocument document = XDocument.Parse(
            """
            <psf2:PrintDeviceCapabilities xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
                                           xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                                           xmlns:psk12="http://schemas.microsoft.com/windows/2013/12/printing/printschemakeywordsv12"
                                           xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                                           xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <psk:PageMediaSize psf2:psftype="Feature">
                <psk:NorthAmericaLetter psf2:psftype="Option" psf2:default="true">
                  <psk:MediaSizeWidth psf2:psftype="ScoredProperty" xsi:type="xsd:integer">215900</psk:MediaSizeWidth>
                  <psk:MediaSizeHeight psf2:psftype="ScoredProperty" xsi:type="xsd:integer">279400</psk:MediaSizeHeight>
                  <psk12:PortraitImageableSize psf2:psftype="Property" xsi:type="psf2:ImageableAreaType">0,0,215900,279400</psk12:PortraitImageableSize>
                </psk:NorthAmericaLetter>
              </psk:PageMediaSize>
            </psf2:PrintDeviceCapabilities>
            """);

        IReadOnlyList<string> messages = PrintDeviceCapabilitiesValidator.Validate(document);

        Assert.Contains(
            "PageMediaSize option 'NorthAmericaLetter' must declare PortraitImageableSize, MediaSizeHeight, and MediaSizeWidth in that order.",
            messages);
    }
}
