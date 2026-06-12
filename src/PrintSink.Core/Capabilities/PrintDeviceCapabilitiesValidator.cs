using System.Xml.Linq;

namespace PrintSink.Core.Capabilities;

/// <summary>
/// Validates the Print Device Capabilities XML shape needed by PrintSink.
/// </summary>
public static class PrintDeviceCapabilitiesValidator
{
    private static readonly XName PsfTypeName = XNamespace.Get(PrintSchemaNamespaces.Framework2) + "psftype";

    /// <summary>
    /// Validates a Print Device Capabilities document.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>Error messages. An empty list means the document passed the current validation gate.</returns>
    public static IReadOnlyList<string> Validate(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<string> messages = [];
        XElement? root = document.Root;
        if (root is null)
        {
            messages.Add("PDC document is empty.");
            return messages;
        }

        if (root.Name.LocalName != "PrintDeviceCapabilities")
        {
            messages.Add("PDC root element must be PrintDeviceCapabilities.");
        }

        if (root.Name.NamespaceName != PrintSchemaNamespaces.Framework2)
        {
            messages.Add("PDC root element must use the Print Schema Framework v2 namespace.");
        }

        List<XElement> features = [.. root
            .Elements()
            .Where(element => string.Equals((string?)element.Attribute(PsfTypeName), "Feature", StringComparison.Ordinal))];

        if (features.Count == 0)
        {
            messages.Add("PDC must contain at least one feature.");
        }

        foreach (XElement feature in features)
        {
            List<XElement> options = [.. feature
                .Elements()
                .Where(element => string.Equals((string?)element.Attribute(PsfTypeName), "Option", StringComparison.Ordinal))];

            if (options.Count == 0)
            {
                messages.Add($"Feature '{feature.Name.LocalName}' must contain at least one option.");
                continue;
            }

            int defaultCount = options.Count(IsDefaultOption);
            if (defaultCount > 1)
            {
                messages.Add($"Feature '{feature.Name.LocalName}' must not contain more than one default option.");
            }
        }

        return messages;
    }

    private static bool IsDefaultOption(XElement option)
    {
        XName defaultName = XNamespace.Get(PrintSchemaNamespaces.Framework2) + "default";
        return string.Equals((string?)option.Attribute(defaultName), "true", StringComparison.OrdinalIgnoreCase);
    }
}
