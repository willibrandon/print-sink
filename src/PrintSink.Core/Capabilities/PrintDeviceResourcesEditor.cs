using System.Xml.Linq;

namespace PrintSink.Core.Capabilities;

/// <summary>
/// Applies localized custom feature display strings to Print Device Resources XML.
/// </summary>
public static class PrintDeviceResourcesEditor
{
    /// <summary>
    /// Applies missing resource strings to a Print Device Resources document.
    /// </summary>
    /// <param name="document">The document to update.</param>
    /// <param name="resources">Resource names and localized values to add when missing.</param>
    /// <returns>A new document with the requested resources present.</returns>
    public static XDocument Apply(XDocument document, IReadOnlyDictionary<string, string> resources)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(resources);

        XDocument result = new(document);
        XElement root = EnsureRoot(result);

        foreach (KeyValuePair<string, string> resource in resources.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resource.Key);
            ArgumentNullException.ThrowIfNull(resource.Value);

            if (FindResource(root, resource.Key) is not null)
            {
                continue;
            }

            root.Add(
                new XElement(
                    "data",
                    new XAttribute("name", resource.Key),
                    new XElement("value", resource.Value)));
        }

        return result;
    }

    private static XElement EnsureRoot(XDocument document)
    {
        if (document.Root is null)
        {
            XElement root = new("root");
            document.Add(root);
            return root;
        }

        if (document.Root.Name.LocalName != "root")
        {
            throw new ArgumentException("PDR root element must be root.", nameof(document));
        }

        return document.Root;
    }

    private static XElement? FindResource(XElement root, string name)
    {
        return root
            .Elements("data")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), name, StringComparison.Ordinal));
    }
}
