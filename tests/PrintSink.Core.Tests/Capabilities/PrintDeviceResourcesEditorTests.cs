using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Core.Tests.Capabilities;

/// <summary>
/// Tests Print Device Resources editing.
/// </summary>
[TestClass]
public sealed class PrintDeviceResourcesEditorTests
{
    /// <summary>
    /// Verifies that missing custom feature resources are appended.
    /// </summary>
    [TestMethod]
    public void Apply_adds_missing_resources()
    {
        XDocument document = CreateMinimalPdr();

        XDocument result = PrintDeviceResourcesEditor.Apply(
            document,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schemas.printsink.dev/printing/keywords/ArchivePaper"] = "Archive paper",
            });

        XElement resource = result.Root!
            .Elements("data")
            .Single(element => (string?)element.Attribute("name") == "schemas.printsink.dev/printing/keywords/ArchivePaper");

        Assert.AreEqual("Archive paper", resource.Element("value")?.Value);
    }

    /// <summary>
    /// Verifies that existing localized strings are not overwritten.
    /// </summary>
    [TestMethod]
    public void Apply_preserves_existing_resources()
    {
        XDocument document = CreateMinimalPdr();

        XDocument result = PrintDeviceResourcesEditor.Apply(
            document,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schemas.printsink.dev/printing/keywords/Dpi600"] = "Six hundred dpi",
            });

        XElement resource = result.Root!
            .Elements("data")
            .Single(element => (string?)element.Attribute("name") == "schemas.printsink.dev/printing/keywords/Dpi600");

        Assert.AreEqual("600 dpi", resource.Element("value")?.Value);
    }

    /// <summary>
    /// Verifies that an empty PDR document can be initialized.
    /// </summary>
    [TestMethod]
    public void Apply_creates_root_for_empty_document()
    {
        XDocument result = PrintDeviceResourcesEditor.Apply(
            new XDocument(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schemas.printsink.dev/printing/keywords/WatermarkMode"] = "Watermark",
            });

        Assert.AreEqual("root", result.Root?.Name.LocalName);
        Assert.IsNotNull(result.Root?.Element("data"));
    }

    private static XDocument CreateMinimalPdr()
    {
        return XDocument.Parse(
            """
            <root>
              <data name="schemas.printsink.dev/printing/keywords/Dpi600">
                <value>600 dpi</value>
              </data>
            </root>
            """);
    }
}
