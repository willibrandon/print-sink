using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Linq;

namespace PrintSink.Tickets;

/// <summary>
/// Maps a focused set of Print Schema ticket features to IPP attributes used by PrintSink workflows.
/// </summary>
public sealed class IppAttributeMapper : IIppAttributeMapper
{
    private static readonly XName FeatureName = XName.Get("Feature", "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework");
    private static readonly XName OptionName = XName.Get("Option", "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework");
    private static readonly XName ValueName = XName.Get("Value", "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework");
    private static readonly XName ParameterInitName = XName.Get("ParameterInit", "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework");
    private static readonly XName NameAttribute = XName.Get("name");
    private static readonly string[] OperationAttributeNames = { "job-password", "job-password-encryption" };

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IppAttributeValue> FromPrintTicket(
        string printTicketXml,
        AttributeMergePolicyOptions options,
        JobPasswordOptions? passwordOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printTicketXml);
        ArgumentNullException.ThrowIfNull(options);

        XDocument ticket = XDocument.Parse(printTicketXml, LoadOptions.PreserveWhitespace);
        Dictionary<string, IppAttributeValue> attributes = new(StringComparer.Ordinal);

        AddFeatureOption(attributes, ticket, "PageOutputColor", "print-color-mode", "keyword", MapColorMode);
        AddFeatureOption(attributes, ticket, "JobDuplexAllDocumentsContiguously", "sides", "keyword", MapDuplexMode);
        AddFeatureOption(attributes, ticket, "PageOrientation", "orientation-requested", "enum", MapOrientation);

        if (options.IncludeCopies)
        {
            AddCopies(attributes, ticket);
        }

        if (!options.RemoveMediaSize)
        {
            AddFeatureOption(attributes, ticket, "PageMediaSize", "media", "keyword", NormalizePrintSchemaToken);
        }

        if (passwordOptions is not null)
        {
            AddJobPassword(attributes, passwordOptions);
        }

        return new ReadOnlyDictionary<string, IppAttributeValue>(attributes);
    }

    private static void AddFeatureOption(
        Dictionary<string, IppAttributeValue> attributes,
        XContainer ticket,
        string printSchemaFeatureSuffix,
        string ippAttribute,
        string syntax,
        Func<string, string> valueMapper)
    {
        XElement? feature = ticket.Descendants(FeatureName).FirstOrDefault(candidate => HasNameSuffix(candidate, printSchemaFeatureSuffix));
        XElement? option = feature?.Elements(OptionName).FirstOrDefault();
        string? optionName = option?.Attribute(NameAttribute)?.Value;

        if (!string.IsNullOrWhiteSpace(optionName))
        {
            attributes[ippAttribute] = IppAttributeValue.CreateString(ippAttribute, syntax, valueMapper(optionName));
        }
    }

    private static void AddCopies(Dictionary<string, IppAttributeValue> attributes, XContainer ticket)
    {
        XElement? parameter = ticket.Descendants(ParameterInitName).FirstOrDefault(candidate => HasNameSuffix(candidate, "JobCopiesAllDocuments"));
        string? copiesText = parameter?.Descendants(ValueName).FirstOrDefault()?.Value;

        if (int.TryParse(copiesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int copies) && copies > 0)
        {
            attributes["copies"] = IppAttributeValue.CreateString("copies", "integer", copies.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddJobPassword(Dictionary<string, IppAttributeValue> attributes, JobPasswordOptions passwordOptions)
    {
        attributes["job-password"] = IppAttributeValue.Binary("job-password", "octetString", passwordOptions.GetEncryptedPassword());
        attributes["job-password-encryption"] = IppAttributeValue.CreateString("job-password-encryption", "keyword", passwordOptions.EncryptionAlgorithm);
        attributes["msft-operation-attribute-col"] = IppAttributeValue.CreateStrings(
            "msft-operation-attribute-col",
            "collection",
            OperationAttributeNames);
    }

    private static bool HasNameSuffix(XElement element, string suffix)
    {
        string? value = element.Attribute(NameAttribute)?.Value;
        return value is not null && value.EndsWith(suffix, StringComparison.Ordinal);
    }

    private static string NormalizePrintSchemaToken(string value)
    {
        int separator = value.IndexOf(':', StringComparison.Ordinal);
        string unqualified = separator >= 0 ? value[(separator + 1)..] : value;
        return unqualified.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string MapColorMode(string value)
    {
        string normalized = NormalizePrintSchemaToken(value);
        return normalized.Contains("monochrome", StringComparison.Ordinal) ? "monochrome" : "color";
    }

    private static string MapDuplexMode(string value)
    {
        string normalized = NormalizePrintSchemaToken(value);
        return normalized switch
        {
            "twosidedlongedge" or "two-sided-long-edge" => "two-sided-long-edge",
            "twosidedshortedge" or "two-sided-short-edge" => "two-sided-short-edge",
            _ => "one-sided",
        };
    }

    private static string MapOrientation(string value)
    {
        string normalized = NormalizePrintSchemaToken(value);
        return normalized switch
        {
            "landscape" => "4",
            "reverselandscape" or "reverse-landscape" => "5",
            "reverseportrait" or "reverse-portrait" => "6",
            _ => "3",
        };
    }
}
