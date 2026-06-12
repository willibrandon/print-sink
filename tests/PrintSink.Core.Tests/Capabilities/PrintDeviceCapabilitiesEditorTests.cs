using System.Xml.Linq;
using PrintSink.Capabilities;

namespace PrintSink.Core.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="PrintDeviceCapabilitiesEditor"/>.
/// </summary>
[TestClass]
internal sealed class PrintDeviceCapabilitiesEditorTests
{
    private const string BaseCapabilitiesXml = """
        <psf:PrintCapabilities
            xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
            xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
          <psf:Feature name="psk:PageOutputColor">
            <psf:Option name="psk:Color" />
          </psf:Feature>
        </psf:PrintCapabilities>
        """;

    /// <summary>
    /// Verifies custom features are injected with PrintSink namespaces and options.
    /// </summary>
    [TestMethod]
    public void ApplyCustomFeatureInsertsFeature()
    {
        PrintDeviceCapabilitiesEditor editor = new();
        CustomFeature feature = new(
            CustomFeatureKind.Watermark,
            "Watermark",
            "Watermark",
            CustomFeatureSelectionMode.PickOne,
            new[]
            {
                new CustomFeatureOption("None", "None"),
                new CustomFeatureOption("Confidential", "Confidential"),
            });

        XDocument result = editor.Apply(XDocument.Parse(BaseCapabilitiesXml), new[] { feature });

        XElement inserted = AssertSingleFeature(result, "printsink:Watermark");
        Assert.AreEqual("https://schemas.printsink.dev/printschema/2026", result.Root?.GetNamespaceOfPrefix("printsink")?.NamespaceName);
        Assert.AreEqual(2, inserted.Elements(PrintDeviceCapabilitiesEditor.PrintSchemaFramework + "Option").Count());
    }

    /// <summary>
    /// Verifies applying the same feature twice is idempotent.
    /// </summary>
    [TestMethod]
    public void ApplySameFeatureTwiceIsIdempotent()
    {
        PrintDeviceCapabilitiesEditor editor = new();
        CustomFeature feature = new(
            CustomFeatureKind.PageOrder,
            "PageOrder",
            "Page order",
            CustomFeatureSelectionMode.PickOne,
            new[]
            {
                new CustomFeatureOption("FrontToBack", "Front to back"),
                new CustomFeatureOption("BackToFront", "Back to front"),
            });

        XDocument once = editor.Apply(XDocument.Parse(BaseCapabilitiesXml), new[] { feature });
        XDocument twice = editor.Apply(once, new[] { feature });

        Assert.AreEqual(1, twice.Descendants(PrintDeviceCapabilitiesEditor.PrintSchemaFramework + "Feature")
            .Count(element => element.Attribute("name")?.Value == "printsink:PageOrder"));
    }

    /// <summary>
    /// Verifies media size options carry width and height scored properties.
    /// </summary>
    [TestMethod]
    public void ApplyMediaSizeFeatureAddsDimensions()
    {
        PrintDeviceCapabilitiesEditor editor = new();
        MediaSize label = new("Label4x6", "4 x 6 in label", 101_600, 152_400);
        CustomFeature feature = new(
            CustomFeatureKind.MediaSize,
            "CustomMediaSize",
            "Custom media size",
            CustomFeatureSelectionMode.PickOne,
            new[] { label.ToFeatureOption() });

        XDocument result = editor.Apply(XDocument.Parse(BaseCapabilitiesXml), new[] { feature });

        XElement inserted = AssertSingleFeature(result, "printsink:CustomMediaSize");
        string xml = inserted.ToString(SaveOptions.DisableFormatting);
        StringAssert.Contains(xml, "printsink:MediaSizeWidth", StringComparison.Ordinal);
        StringAssert.Contains(xml, "101600", StringComparison.Ordinal);
        StringAssert.Contains(xml, "printsink:MediaSizeHeight", StringComparison.Ordinal);
        StringAssert.Contains(xml, "152400", StringComparison.Ordinal);
    }

    private static XElement AssertSingleFeature(XDocument document, string featureName)
    {
        XElement[] features = document.Descendants(PrintDeviceCapabilitiesEditor.PrintSchemaFramework + "Feature")
            .Where(element => element.Attribute("name")?.Value == featureName)
            .ToArray();

        Assert.AreEqual(1, features.Length);
        return features[0];
    }
}
