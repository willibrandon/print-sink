using System.Xml;
using System.Xml.Linq;

namespace PrintSink.Cli;

internal static class PdcValidator
{
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

        if (document.Root?.Name.LocalName != "PrintDeviceCapabilities")
        {
            messages.Add("error: PDC root element must be PrintDeviceCapabilities.");
        }

        if (messages.Count == 0)
        {
            messages.Add("ok: PDC XML shape is valid.");
        }

        return new ValidationResult(messages.All(message => !message.StartsWith("error:", StringComparison.Ordinal)), messages);
    }
}
