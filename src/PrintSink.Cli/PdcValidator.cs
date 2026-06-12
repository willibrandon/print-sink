using System.Xml;
using System.Xml.Linq;
using PrintSink.Core.Capabilities;

namespace PrintSink.Cli;

/// <summary>
/// Validates Print Device Capabilities XML fixtures.
/// </summary>
internal static class PdcValidator
{
    /// <summary>
    /// Validates basic PDC XML shape.
    /// </summary>
    /// <param name="pdcPath">The PDC file path.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult Validate(string pdcPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdcPath);

        List<string> messages = [];

        if (!File.Exists(pdcPath))
        {
            messages.Add($"error: PDC file not found: {pdcPath}");
            return new ValidationResult(false, messages);
        }

        XDocument document;
        try
        {
            document = XDocument.Load(pdcPath, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            messages.Add($"error: PDC XML is invalid: {ex.Message}");
            return new ValidationResult(false, messages);
        }

        IReadOnlyList<string> coreMessages = PrintDeviceCapabilitiesValidator.Validate(document);
        foreach (string message in coreMessages)
        {
            messages.Add($"error: {message}");
        }

        if (messages.Count == 0)
        {
            messages.Add("ok: PDC XML shape is valid.");
        }

        return new ValidationResult(messages.All(message => !message.StartsWith("error:", StringComparison.Ordinal)), messages);
    }
}
