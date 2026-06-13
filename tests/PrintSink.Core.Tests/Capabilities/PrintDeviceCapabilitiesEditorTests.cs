using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Core.Tests.Capabilities;

/// <summary>
/// Tests Print Device Capabilities editing.
/// </summary>
[TestClass]
public sealed class PrintDeviceCapabilitiesEditorTests
{
    private static readonly XNamespace Psf2 = PrintSchemaNamespaces.Framework2;
    private static readonly XNamespace Psk = PrintSchemaNamespaces.Keywords;
    private static readonly XNamespace PrintSink = "https://schemas.printsink.dev/printing/keywords";

    private readonly PrintDeviceCapabilitiesEditor editor = new();

    /// <summary>
    /// Verifies that a custom option is appended without replacing existing options.
    /// </summary>
    [TestMethod]
    public void Apply_adds_custom_option_to_existing_feature()
    {
        XDocument document = CreateMinimalPdc();
        CustomFeature feature = new(
            PrintSchemaQualifiedName.Keyword("PageMediaType"),
            [
                new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ArchivePaper"), false, []),
            ]);

        XDocument result = editor.Apply(document, [feature]);

        XElement pageMediaType = result.Root!.Element(Psk + "PageMediaType")!;
        Assert.IsNotNull(pageMediaType.Element(PrintSink + "ArchivePaper"));
        Assert.AreEqual("https://schemas.printsink.dev/printing/keywords", result.Root!.Attribute(XNamespace.Xmlns + "printsink")?.Value);
    }

    /// <summary>
    /// Verifies that applying the same feature twice does not duplicate options.
    /// </summary>
    [TestMethod]
    public void Apply_is_idempotent_for_existing_options()
    {
        XDocument document = CreateMinimalPdc();
        CustomFeature feature = new(
            PrintSchemaQualifiedName.Keyword("PageMediaType"),
            [
                new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ArchivePaper"), false, []),
            ]);

        XDocument once = editor.Apply(document, [feature]);
        XDocument twice = editor.Apply(once, [feature]);

        int optionCount = twice.Root!
            .Element(Psk + "PageMediaType")!
            .Elements(PrintSink + "ArchivePaper")
            .Count();

        Assert.AreEqual(1, optionCount);
    }

    /// <summary>
    /// Verifies that a custom default option clears the previous default option.
    /// </summary>
    [TestMethod]
    public void Apply_moves_default_to_custom_option()
    {
        XDocument document = CreateMinimalPdc();
        CustomFeature feature = new(
            PrintSchemaQualifiedName.Keyword("PageMediaType"),
            [
                new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ArchivePaper"), true, []),
            ]);

        XDocument result = editor.Apply(document, [feature]);

        XElement featureElement = result.Root!.Element(Psk + "PageMediaType")!;
        Assert.AreEqual("false", featureElement.Element(Psk + "AutoSelect")!.Attribute(Psf2 + "default")?.Value);
        Assert.AreEqual(
            "true",
            featureElement.Element(PrintSink + "ArchivePaper")!.Attribute(Psf2 + "default")?.Value);
    }

    /// <summary>
    /// Verifies that media-size options carry Print Schema scored properties.
    /// </summary>
    [TestMethod]
    public void Apply_adds_media_size_scored_properties()
    {
        XDocument document = CreateMinimalPdc();
        CustomFeature feature = new(
            PrintSchemaQualifiedName.Keyword("PageMediaSize"),
            [
                new CustomFeatureOption(
                        PrintSchemaQualifiedName.PrintSink("Receipt80Millimeter"),
                        false,
                        [
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword12("PortraitImageableSize"), PrintSchemaPropertyKind.Property, "0,0,80000,200000", "psf2:ImageableAreaType"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("MediaSizeHeight"), PrintSchemaPropertyKind.ScoredProperty, "200000", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("MediaSizeWidth"), PrintSchemaPropertyKind.ScoredProperty, "80000", "xsd:integer"),
                        ]),
            ]);

        XDocument result = editor.Apply(document, [feature]);

        XElement option = result.Root!
            .Element(Psk + "PageMediaSize")!
            .Element(PrintSink + "Receipt80Millimeter")!;

        Assert.AreEqual("80000", option.Element(Psk + "MediaSizeWidth")?.Value);
        Assert.AreEqual("200000", option.Element(Psk + "MediaSizeHeight")?.Value);
        Assert.AreEqual("0,0,80000,200000", option.Element(XNamespace.Get(PrintSchemaNamespaces.Keywords12) + "PortraitImageableSize")?.Value);
        CollectionAssert.AreEqual(
            new[] { "PortraitImageableSize", "MediaSizeHeight", "MediaSizeWidth" },
            option.Elements().Select(static element => element.Name.LocalName).ToArray());
    }

    /// <summary>
    /// Verifies that the shared PrintSink feature set injects the custom capabilities used by the package.
    /// </summary>
    [TestMethod]
    public void Apply_adds_built_in_printsink_features()
    {
        XDocument document = CreateMinimalPdc();

        XDocument result = editor.Apply(document, PrintSinkCapabilityFeatures.BuiltIn);

        XElement pageMediaSize = result.Root!.Element(Psk + "PageMediaSize")!;
        XElement receipt80Millimeter = pageMediaSize.Element(PrintSink + "Receipt80Millimeter")!;
        Assert.IsNotNull(receipt80Millimeter);
        Assert.AreEqual("80000", receipt80Millimeter.Element(Psk + "MediaSizeWidth")?.Value);
        Assert.AreEqual("200000", receipt80Millimeter.Element(Psk + "MediaSizeHeight")?.Value);

        XElement pageMediaType = result.Root.Element(Psk + "PageMediaType")!;
        Assert.IsNotNull(pageMediaType.Element(PrintSink + "ArchivePaper"));
        Assert.IsNotNull(pageMediaType.Element(PrintSink + "ThermalReceiptMedia"));

        XElement jobInputBin = result.Root.Element(Psk + "JobInputBin")!;
        Assert.IsNotNull(jobInputBin.Element(PrintSink + "AutomationInputBin"));

        XElement jobOutputBin = result.Root.Element(Psk + "JobOutputBin")!;
        Assert.IsNotNull(jobOutputBin.Element(PrintSink + "AutomationOutputBin"));

        XElement jobPageOrder = result.Root.Element(Psk + "JobPageOrder")!;
        Assert.IsNotNull(jobPageOrder.Element(PrintSink + "OddPagesThenEvenPages"));

        XElement jobStaple = result.Root.Element(Psk + "JobStapleAllDocuments")!;
        Assert.IsNotNull(jobStaple.Element(PrintSink + "StapleUpperLeft"));

        XElement pageResolution = result.Root!.Element(Psk + "PageResolution")!;
        XElement dpi600 = pageResolution.Element(PrintSink + "Dpi600")!;
        Assert.IsNotNull(dpi600);
        Assert.AreEqual("true", dpi600.Attribute(Psf2 + "default")?.Value);
        Assert.AreEqual("600", dpi600.Element(Psk + "ResolutionX")?.Value);
        Assert.AreEqual("600", dpi600.Element(Psk + "ResolutionY")?.Value);
        XElement dpi1200 = pageResolution.Element(PrintSink + "Dpi1200")!;
        Assert.IsNotNull(dpi1200);
        Assert.AreEqual("1200", dpi1200.Element(Psk + "ResolutionX")?.Value);
        Assert.AreEqual("1200", dpi1200.Element(Psk + "ResolutionY")?.Value);

        XElement watermarkMode = result.Root.Element(PrintSink + "JobWatermarkMode")!;
        Assert.IsNotNull(watermarkMode.Element(PrintSink + "WatermarkOff"));
        Assert.IsNotNull(watermarkMode.Element(PrintSink + "WatermarkText"));
        Assert.IsNotNull(watermarkMode.Element(PrintSink + "WatermarkImage"));
    }

    private static XDocument CreateMinimalPdc()
    {
        return XDocument.Parse(
            """
            <psf2:PrintDeviceCapabilities xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
                                           xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                                           xmlns:psk12="http://schemas.microsoft.com/windows/2013/12/printing/printschemakeywordsv12"
                                           xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                                           xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <psk:PageMediaType psf2:psftype="Feature">
                <psk:AutoSelect psf2:psftype="Option" psf2:default="true" />
              </psk:PageMediaType>
              <psk:PageMediaSize psf2:psftype="Feature">
                <psk:NorthAmericaLetter psf2:psftype="Option" psf2:default="true">
                  <psk:MediaSizeWidth psf2:psftype="ScoredProperty" xsi:type="xsd:integer">215900</psk:MediaSizeWidth>
                  <psk:MediaSizeHeight psf2:psftype="ScoredProperty" xsi:type="xsd:integer">279400</psk:MediaSizeHeight>
                </psk:NorthAmericaLetter>
              </psk:PageMediaSize>
            </psf2:PrintDeviceCapabilities>
            """);
    }
}
