using System.Xml;
using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Cli;

/// <summary>
/// Validates Print Device Capabilities XML fixtures.
/// </summary>
internal static class PdcValidator
{
    private const string PrintSinkKeywordNamespace = "https://schemas.printsink.dev/printing/keywords";

    /// <summary>
    /// Validates PDC XML shape.
    /// </summary>
    /// <param name="pdcPath">The PDC file path.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult Validate(string pdcPath)
    {
        return Validate(pdcPath, null);
    }

    /// <summary>
    /// Validates PDC XML shape and, when present, matching PDR resources.
    /// </summary>
    /// <param name="pdcPath">The PDC file path.</param>
    /// <param name="pdrPath">The optional PDR file path.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult Validate(string pdcPath, string? pdrPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdcPath);

        List<string> messages = [];

        XDocument? pdcDocument = TryLoadXml(pdcPath, "PDC", messages);
        if (pdcDocument is not null)
        {
            IReadOnlyList<string> coreMessages = PrintDeviceCapabilitiesValidator.Validate(pdcDocument);
            foreach (string message in coreMessages)
            {
                messages.Add($"error: {message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(pdrPath))
        {
            ValidatePrintDeviceResources(pdrPath, pdcDocument, messages);
        }

        if (messages.Count == 0)
        {
            messages.Add(string.IsNullOrWhiteSpace(pdrPath)
                ? "ok: PDC XML shape is valid."
                : "ok: PDC/PDR XML shape is valid.");
        }

        return new ValidationResult(messages.All(message => !message.StartsWith("error:", StringComparison.Ordinal)), messages);
    }

    private static void ValidatePrintDeviceResources(
        string pdrPath,
        XDocument? pdcDocument,
        List<string> messages)
    {
        XDocument? pdrDocument = TryLoadXml(pdrPath, "PDR", messages);
        if (pdrDocument is null)
        {
            return;
        }

        if (pdrDocument.Root?.Name.LocalName != "root")
        {
            messages.Add("error: PDR root element must be root.");
        }

        HashSet<string> pdrResourceNames = pdrDocument
            .Descendants("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (string duplicateName in GetDuplicateResourceNames(pdrDocument))
        {
            messages.Add($"error: PDR resource '{duplicateName}' is duplicated.");
        }

        if (pdcDocument is null)
        {
            return;
        }

        foreach (string resourceName in GetRequiredPrintSinkResourceNames(pdcDocument))
        {
            if (!pdrResourceNames.Contains(resourceName))
            {
                messages.Add($"error: PDR is missing resource '{resourceName}'.");
            }
        }
    }

    private static XDocument? TryLoadXml(string path, string displayName, List<string> messages)
    {
        if (!File.Exists(path))
        {
            messages.Add($"error: {displayName} file not found: {path}");
            return null;
        }

        try
        {
            return XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            messages.Add($"error: {displayName} XML is invalid: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<string> GetDuplicateResourceNames(XDocument pdrDocument)
    {
        return pdrDocument
            .Descendants("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetRequiredPrintSinkResourceNames(XDocument pdcDocument)
    {
        return pdcDocument
            .Descendants()
            .Where(static element => element.Name.NamespaceName == PrintSinkKeywordNamespace)
            .Select(static element => ToPdrResourceName(element.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }

    private static string ToPdrResourceName(XName name)
    {
        return string.Concat(name.NamespaceName["https://".Length..].TrimEnd('/'), "/", name.LocalName);
    }
}
