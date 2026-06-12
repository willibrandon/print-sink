using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PrintSink.Core.Tickets;

namespace PrintSink.Cli;

/// <summary>
/// Reads print-ticket fixtures for CLI inspection.
/// </summary>
internal static class TicketMapper
{
    /// <summary>
    /// Summarizes a print-ticket fixture.
    /// </summary>
    /// <param name="ticketPath">The print-ticket file path.</param>
    /// <returns>The print-ticket summary result.</returns>
    public static TicketMapResult Map(string ticketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketPath);

        List<string> messages = [];

        if (!File.Exists(ticketPath))
        {
            messages.Add($"error: print ticket file not found: {ticketPath}");
            return new TicketMapResult(false, messages);
        }

        XDocument document;
        try
        {
            document = XDocument.Load(ticketPath, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            messages.Add($"error: print ticket XML is invalid: {ex.Message}");
            return new TicketMapResult(false, messages);
        }

        int featureCount = document.Descendants().Count(element => element.Name.LocalName == "Feature");
        int optionCount = document.Descendants().Count(element => element.Name.LocalName == "Option");
        int parameterCount = document.Descendants().Count(element => element.Name.LocalName == "ParameterInit");
        IReadOnlyDictionary<string, IppAttributeValue> attributes = new IppAttributeMapper().FromPrintTicket(document);

        messages.Add("ok: print ticket XML parsed.");
        messages.Add(string.Create(CultureInfo.InvariantCulture, $"Features: {featureCount}"));
        messages.Add(string.Create(CultureInfo.InvariantCulture, $"Options: {optionCount}"));
        messages.Add(string.Create(CultureInfo.InvariantCulture, $"Parameters: {parameterCount}"));
        messages.Add(string.Create(CultureInfo.InvariantCulture, $"IPP attributes: {attributes.Count}"));

        foreach (IppAttributeValue attribute in attributes.Values.OrderBy(attribute => attribute.Name, StringComparer.OrdinalIgnoreCase))
        {
            messages.Add($"{attribute.Name}: {string.Join(", ", attribute.Values)}");
        }

        return new TicketMapResult(true, messages);
    }
}
