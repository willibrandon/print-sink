using System.Xml.Linq;

namespace PrintSink.Core.Capabilities;

/// <summary>
/// Applies custom feature options to Print Device Capabilities XML.
/// </summary>
public sealed class PrintDeviceCapabilitiesEditor : IPrintDeviceCapabilitiesEditor
{
    private static readonly XNamespace Psf2Namespace = PrintSchemaNamespaces.Framework2;
    private static readonly XNamespace XsiNamespace = PrintSchemaNamespaces.XmlSchemaInstance;
    private static readonly XName PsfTypeName = Psf2Namespace + "psftype";
    private static readonly XName DefaultName = Psf2Namespace + "default";
    private static readonly XName XsiTypeName = XsiNamespace + "type";

    /// <inheritdoc />
    public XDocument Apply(XDocument document, IReadOnlyList<CustomFeature> features)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(features);

        XDocument result = new(document);
        XElement root = result.Root ?? throw new ArgumentException("PDC document is empty.", nameof(document));
        if (root.Name.LocalName != "PrintDeviceCapabilities")
        {
            throw new ArgumentException("PDC root element must be PrintDeviceCapabilities.", nameof(document));
        }

        if (root.Name.NamespaceName != PrintSchemaNamespaces.Framework2)
        {
            throw new ArgumentException("PDC root element must use the Print Schema Framework v2 namespace.", nameof(document));
        }

        EnsureNamespaceDeclaration(root, "psf2", PrintSchemaNamespaces.Framework2);

        foreach (CustomFeature feature in features)
        {
            ApplyFeature(root, feature);
        }

        return result;
    }

    private static void ApplyFeature(XElement root, CustomFeature feature)
    {
        EnsureNamespaceDeclaration(root, feature.Name);

        XElement featureElement = root
            .Elements()
            .FirstOrDefault(element => element.Name == feature.Name.ToXName())
            ?? CreateFeature(root, feature.Name);

        featureElement.SetAttributeValue(PsfTypeName, "Feature");

        foreach (CustomFeatureOption option in feature.Options)
        {
            ApplyOption(root, featureElement, option);
        }
    }

    private static XElement CreateFeature(XElement root, PrintSchemaQualifiedName name)
    {
        XElement featureElement = new(name.ToXName());
        root.Add(featureElement);
        return featureElement;
    }

    private static void ApplyOption(XElement root, XElement featureElement, CustomFeatureOption option)
    {
        EnsureNamespaceDeclaration(root, option.Name);

        XElement optionElement = featureElement
            .Elements()
            .FirstOrDefault(element => element.Name == option.Name.ToXName())
            ?? CreateOption(featureElement, option.Name);

        optionElement.SetAttributeValue(PsfTypeName, "Option");
        optionElement.SetAttributeValue(DefaultName, option.IsDefault ? "true" : "false");

        if (option.IsDefault)
        {
            ClearSiblingDefaults(featureElement, optionElement);
        }

        foreach (PrintSchemaProperty property in option.Properties)
        {
            ApplyProperty(root, optionElement, property);
        }
    }

    private static XElement CreateOption(XElement featureElement, PrintSchemaQualifiedName name)
    {
        XElement optionElement = new(name.ToXName());
        featureElement.Add(optionElement);
        return optionElement;
    }

    private static void ApplyProperty(XElement root, XElement optionElement, PrintSchemaProperty property)
    {
        EnsureNamespaceDeclaration(root, property.Name);

        if (!string.IsNullOrWhiteSpace(property.XsiType))
        {
            EnsureNamespaceDeclaration(root, "xsi", PrintSchemaNamespaces.XmlSchemaInstance);
            EnsureNamespaceDeclaration(root, "xsd", PrintSchemaNamespaces.XmlSchema);
        }

        XElement propertyElement = optionElement
            .Elements()
            .FirstOrDefault(element => element.Name == property.Name.ToXName())
            ?? CreateProperty(optionElement, property.Name);

        propertyElement.SetAttributeValue(PsfTypeName, property.Kind.ToString());
        propertyElement.SetAttributeValue(XsiTypeName, property.XsiType);
        propertyElement.Value = property.Value;
    }

    private static XElement CreateProperty(XElement optionElement, PrintSchemaQualifiedName name)
    {
        XElement propertyElement = new(name.ToXName());
        optionElement.Add(propertyElement);
        return propertyElement;
    }

    private static void ClearSiblingDefaults(XElement featureElement, XElement defaultOption)
    {
        foreach (XElement sibling in featureElement.Elements().Where(element => element != defaultOption))
        {
            if (string.Equals((string?)sibling.Attribute(PsfTypeName), "Option", StringComparison.Ordinal))
            {
                sibling.SetAttributeValue(DefaultName, "false");
            }
        }
    }

    private static void EnsureNamespaceDeclaration(XElement root, PrintSchemaQualifiedName name)
    {
        EnsureNamespaceDeclaration(root, name.Prefix, name.NamespaceUri);
    }

    private static void EnsureNamespaceDeclaration(XElement root, string prefix, string namespaceUri)
    {
        XNamespace namespaceDeclaration = XNamespace.Xmlns;
        XAttribute? existing = root.Attribute(namespaceDeclaration + prefix);
        if (existing is null)
        {
            root.Add(new XAttribute(namespaceDeclaration + prefix, namespaceUri));
            return;
        }

        if (!string.Equals(existing.Value, namespaceUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Namespace prefix '{prefix}' is already bound to '{existing.Value}'.");
        }
    }
}
