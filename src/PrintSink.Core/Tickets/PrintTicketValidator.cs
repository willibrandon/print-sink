using System.Xml.Linq;

namespace PrintSink.Core.Tickets;

/// <summary>
/// Validates the Print Schema ticket shape that PrintSink can safely route.
/// </summary>
public static class PrintTicketValidator
{
    /// <summary>
    /// Validates a print ticket document.
    /// </summary>
    /// <param name="printTicket">The print ticket document.</param>
    /// <returns>Error messages. An empty list means the ticket passed the current validation gate.</returns>
    public static IReadOnlyList<string> Validate(XDocument printTicket)
    {
        ArgumentNullException.ThrowIfNull(printTicket);

        List<string> messages = [];
        XElement? root = printTicket.Root;
        if (root is null)
        {
            messages.Add("Print ticket document is empty.");
            return messages;
        }

        if (root.Name.LocalName != "PrintTicket")
        {
            messages.Add("Print ticket root element must be PrintTicket.");
        }

        foreach (XElement feature in root.Descendants().Where(element => element.Name.LocalName == "Feature"))
        {
            ValidateFeature(feature, messages);
        }

        foreach (XElement parameter in root.Descendants().Where(element => element.Name.LocalName == "ParameterInit"))
        {
            ValidateParameter(parameter, messages);
        }

        return messages;
    }

    private static void ValidateFeature(XElement feature, List<string> messages)
    {
        string featureName = GetPrintSchemaName(feature) ?? "<unnamed>";
        if (featureName == "<unnamed>")
        {
            messages.Add("Print ticket feature is missing a name.");
        }

        int optionCount = feature
            .Elements()
            .Count(element => element.Name.LocalName == "Option");
        if (optionCount > 1)
        {
            messages.Add($"Print ticket feature '{featureName}' has more than one selected option.");
        }
    }

    private static void ValidateParameter(XElement parameter, List<string> messages)
    {
        string parameterName = GetPrintSchemaName(parameter) ?? "<unnamed>";
        if (parameterName == "<unnamed>")
        {
            messages.Add("Print ticket parameter is missing a name.");
        }

        string? value = parameter
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Value")
            ?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add($"Print ticket parameter '{parameterName}' is missing a value.");
        }
    }

    private static string? GetPrintSchemaName(XElement element)
    {
        string? name = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        int prefixSeparator = name.IndexOf(':', StringComparison.Ordinal);
        return prefixSeparator < 0 ? name : name[(prefixSeparator + 1)..];
    }
}
