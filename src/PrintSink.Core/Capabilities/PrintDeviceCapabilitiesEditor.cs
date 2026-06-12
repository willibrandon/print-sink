using System.Xml.Linq;

namespace PrintSink.Capabilities;

/// <summary>
/// Mutates Print Device Capabilities XML with PrintSink custom feature declarations.
/// </summary>
public sealed class PrintDeviceCapabilitiesEditor : IPrintDeviceCapabilitiesEditor
{
    /// <summary>
    /// The Print Schema Framework namespace.
    /// </summary>
    public static readonly XNamespace PrintSchemaFramework = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";

    /// <summary>
    /// The Print Schema Keywords namespace.
    /// </summary>
    public static readonly XNamespace PrintSchemaKeywords = "http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";

    /// <summary>
    /// The XML Schema instance namespace.
    /// </summary>
    public static readonly XNamespace XmlSchemaInstance = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// The XML Schema namespace.
    /// </summary>
    public static readonly XNamespace XmlSchema = "http://www.w3.org/2001/XMLSchema";

    /// <summary>
    /// The PrintSink custom Print Schema namespace.
    /// </summary>
    public static readonly XNamespace PrintSinkNamespace = "https://schemas.printsink.dev/printschema/2026";

    /// <inheritdoc />
    public XDocument Apply(XDocument capabilities, IReadOnlyList<CustomFeature> features)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(features);

        XDocument edited = new(capabilities);
        XElement root = edited.Root ?? throw new InvalidOperationException("Print Device Capabilities XML must have a root element.");
        EnsureNamespaces(root);

        foreach (CustomFeature feature in features)
        {
            RemoveExistingFeature(root, feature.Name);
            root.Add(CreateFeatureElement(feature));
        }

        return edited;
    }

    private static void EnsureNamespaces(XElement root)
    {
        root.SetAttributeValue(XNamespace.Xmlns + "psf", PrintSchemaFramework.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "psk", PrintSchemaKeywords.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "xsi", XmlSchemaInstance.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "xsd", XmlSchema.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "printsink", PrintSinkNamespace.NamespaceName);
    }

    private static void RemoveExistingFeature(XElement root, string featureName)
    {
        string qualifiedName = Qualify(featureName);
        root.Elements(PrintSchemaFramework + "Feature")
            .Where(element => string.Equals(element.Attribute("name")?.Value, qualifiedName, StringComparison.Ordinal))
            .Remove();
    }

    private static XElement CreateFeatureElement(CustomFeature feature)
    {
        XElement element = new(
            PrintSchemaFramework + "Feature",
            new XAttribute("name", Qualify(feature.Name)),
            CreateProperty("DisplayName", feature.DisplayName, "string"),
            CreateProperty("SelectionType", SelectionModeKeyword(feature.SelectionMode), "QName"),
            CreateProperty("PrintSinkFeatureKind", feature.Kind.ToString(), "string"));

        foreach (CustomFeatureOption option in feature.Options)
        {
            element.Add(CreateOptionElement(option));
        }

        return element;
    }

    private static XElement CreateOptionElement(CustomFeatureOption option)
    {
        XElement element = new(
            PrintSchemaFramework + "Option",
            new XAttribute("name", Qualify(option.Name)),
            CreateProperty("DisplayName", option.DisplayName, "string"));

        foreach (KeyValuePair<string, string> scoredProperty in option.ScoredProperties)
        {
            element.Add(CreateScoredProperty(scoredProperty.Key, scoredProperty.Value));
        }

        return element;
    }

    private static XElement CreateProperty(string name, string value, string typeName)
    {
        return new XElement(
            PrintSchemaFramework + "Property",
            new XAttribute("name", QualifyFrameworkName(name)),
            new XElement(
                PrintSchemaFramework + "Value",
                new XAttribute(XmlSchemaInstance + "type", "xsd:" + typeName),
                value));
    }

    private static XElement CreateScoredProperty(string name, string value)
    {
        return new XElement(
            PrintSchemaFramework + "ScoredProperty",
            new XAttribute("name", Qualify(name)),
            new XElement(
                PrintSchemaFramework + "Value",
                new XAttribute(XmlSchemaInstance + "type", "xsd:string"),
                value));
    }

    private static string SelectionModeKeyword(CustomFeatureSelectionMode mode)
    {
        return mode switch
        {
            CustomFeatureSelectionMode.PickOne => "psk:PickOne",
            CustomFeatureSelectionMode.PickMany => "psk:PickMany",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported selection mode."),
        };
    }

    private static string Qualify(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.Contains(':', StringComparison.Ordinal) ? name : "printsink:" + name;
    }

    private static string QualifyFrameworkName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.Contains(':', StringComparison.Ordinal) ? name : "psf:" + name;
    }
}
