using System.Xml;
using System.Xml.Linq;

namespace PrintSink.Cli;

internal static class ManifestLinter
{
    private static readonly string[] RequiredExtensionCategories =
    [
        "windows.printSupportVirtualPrinterWorkflow",
        "windows.printSupportExtension",
        "windows.printSupportSettingsUI",
        "windows.printSupportJobUI",
    ];

    public static ManifestLintResult Lint(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        List<string> messages = [];

        if (!File.Exists(manifestPath))
        {
            messages.Add($"error: manifest file not found: {manifestPath}");
            return new ManifestLintResult(false, messages);
        }

        XDocument document;
        try
        {
            document = XDocument.Load(manifestPath, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            messages.Add($"error: manifest XML is invalid: {ex.Message}");
            return new ManifestLintResult(false, messages);
        }

        XElement? package = document.Root;
        if (package?.Name.LocalName != "Package")
        {
            messages.Add("error: root element must be Package.");
            return new ManifestLintResult(false, messages);
        }

        XElement? identity = package.Elements().FirstOrDefault(element => element.Name.LocalName == "Identity");
        AddRequiredAttributeMessage(identity, "Name", "package identity name", messages);
        AddRequiredAttributeMessage(identity, "Publisher", "package publisher", messages);
        AddRequiredAttributeMessage(identity, "Version", "package version", messages);

        XElement? displayName = package
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Properties")
            ?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName?.Value))
        {
            messages.Add("error: package DisplayName is required.");
        }

        HashSet<string> capabilities = package
            .Descendants()
            .Where(element => element.Name.LocalName == "Capability")
            .Select(element => (string?)element.Attribute("Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        if (!capabilities.Contains("runFullTrust"))
        {
            messages.Add("error: runFullTrust capability is required for the packaged foreground app.");
        }

        if (capabilities.Contains("systemAIModels"))
        {
            messages.Add("error: systemAIModels capability is not part of the PrintSink package shape.");
        }

        HashSet<string> extensionCategories = package
            .Descendants()
            .Where(element => element.Name.LocalName == "Extension")
            .Select(element => (string?)element.Attribute("Category"))
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        foreach (string category in RequiredExtensionCategories)
        {
            if (!extensionCategories.Contains(category))
            {
                messages.Add($"error: missing {category} extension.");
            }
        }

        if (messages.Count == 0)
        {
            messages.Add("ok: manifest package shape is valid.");
        }

        return new ManifestLintResult(messages.All(message => !message.StartsWith("error:", StringComparison.Ordinal)), messages);
    }

    private static void AddRequiredAttributeMessage(
        XElement? element,
        string attributeName,
        string displayName,
        ICollection<string> messages)
    {
        if (string.IsNullOrWhiteSpace((string?)element?.Attribute(attributeName)))
        {
            messages.Add($"error: {displayName} is required.");
        }
    }
}
