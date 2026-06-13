using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Core.Tests.Capabilities;

/// <summary>
/// Tests Print Device Resources editing.
/// </summary>
[TestClass]
internal sealed class PrintDeviceResourcesEditorTests
{
    /// <summary>
    /// Verifies that missing custom feature resources are appended.
    /// </summary>
    [TestMethod]
    public void ApplyAddsMissingResources()
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
    public void ApplyPreservesExistingResources()
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
    public void ApplyCreatesRootForEmptyDocument()
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
