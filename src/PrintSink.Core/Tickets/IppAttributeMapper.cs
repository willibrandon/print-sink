using System.Xml.Linq;

namespace PrintSink.Core.Tickets;

/// <summary>
/// Maps common Print Schema ticket selections into IPP job attributes.
/// </summary>
public sealed class IppAttributeMapper : IIppAttributeMapper
{
    private readonly Dictionary<string, Func<string, IppAttributeValue?>> featureMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="IppAttributeMapper"/> class.
    /// </summary>
    public IppAttributeMapper()
    {
        featureMappings = new Dictionary<string, Func<string, IppAttributeValue?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PageMediaSize"] = option => IppAttributeValue.Single("media", NormalizeKeyword(option)),
            ["PageMediaType"] = option => IppAttributeValue.Single("media-type", NormalizeKeyword(option)),
            ["JobInputBin"] = option => IppAttributeValue.Single("media-source", NormalizeKeyword(option)),
            ["JobDuplexAllDocumentsContiguously"] = MapDuplex,
            ["PageOutputColor"] = MapColor,
            ["PageOrientation"] = MapOrientation,
            ["PageOutputQuality"] = MapQuality,
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IppAttributeValue> FromPrintTicket(XDocument printTicket)
    {
        ArgumentNullException.ThrowIfNull(printTicket);

        Dictionary<string, IppAttributeValue> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (XElement featureElement in printTicket.Descendants().Where(element => element.Name.LocalName == "Feature"))
        {
            string? featureName = GetPrintSchemaName(featureElement);
            string? optionName = GetSelectedOptionName(featureElement);
            if (featureName is null || optionName is null)
            {
                continue;
            }

            if (featureMappings.TryGetValue(featureName, out Func<string, IppAttributeValue?>? mapper))
            {
                IppAttributeValue? attribute = mapper(optionName);
                if (attribute is not null)
                {
                    attributes[attribute.Name] = attribute;
                }
            }
        }

        foreach (XElement parameterElement in printTicket.Descendants().Where(element => element.Name.LocalName == "ParameterInit"))
        {
            string? parameterName = GetPrintSchemaName(parameterElement);
            string? value = parameterElement
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName == "Value")
                ?.Value;

            if (string.IsNullOrWhiteSpace(parameterName) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            AddParameterAttribute(attributes, parameterName, value);
        }

        return attributes;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IppAttributeValue> ApplyMergePolicy(
        IReadOnlyDictionary<string, IppAttributeValue> attributes,
        AttributeMergePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(options);

        Dictionary<string, IppAttributeValue> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IppAttributeValue> attribute in attributes)
        {
            if (!options.ShouldRemove(attribute.Key))
            {
                result[attribute.Key] = attribute.Value;
            }
        }

        return result;
    }

    private static void AddParameterAttribute(
        Dictionary<string, IppAttributeValue> attributes,
        string parameterName,
        string value)
    {
        switch (parameterName)
        {
            case "JobCopiesAllDocuments":
                attributes["copies"] = IppAttributeValue.Single("copies", value);
                break;
            case "JobNUpAllDocumentsContiguously":
                attributes["number-up"] = IppAttributeValue.Single("number-up", value);
                break;
        }
    }

    private static string? GetPrintSchemaName(XElement element)
    {
        string? name = (string?)element.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return element.Name.LocalName;
        }

        int prefixSeparator = name.IndexOf(':', StringComparison.Ordinal);
        return prefixSeparator < 0 ? name : name[(prefixSeparator + 1)..];
    }

    private static string? GetSelectedOptionName(XElement featureElement)
    {
        XElement? optionElement = featureElement.Elements().FirstOrDefault(element => element.Name.LocalName == "Option");
        return optionElement is null ? null : GetPrintSchemaName(optionElement);
    }

    private static IppAttributeValue? MapColor(string option)
    {
        return option switch
        {
            "Color" => IppAttributeValue.Single("print-color-mode", "color"),
            "Monochrome" or "Grayscale" => IppAttributeValue.Single("print-color-mode", "monochrome"),
            _ => IppAttributeValue.Single("print-color-mode", NormalizeKeyword(option)),
        };
    }

    private static IppAttributeValue? MapDuplex(string option)
    {
        return option switch
        {
            "OneSided" => IppAttributeValue.Single("sides", "one-sided"),
            "TwoSidedLongEdge" => IppAttributeValue.Single("sides", "two-sided-long-edge"),
            "TwoSidedShortEdge" => IppAttributeValue.Single("sides", "two-sided-short-edge"),
            _ => null,
        };
    }

    private static IppAttributeValue? MapOrientation(string option)
    {
        return option switch
        {
            "Portrait" => IppAttributeValue.Single("orientation-requested", "3"),
            "Landscape" => IppAttributeValue.Single("orientation-requested", "4"),
            "ReverseLandscape" => IppAttributeValue.Single("orientation-requested", "5"),
            "ReversePortrait" => IppAttributeValue.Single("orientation-requested", "6"),
            _ => null,
        };
    }

    private static IppAttributeValue? MapQuality(string option)
    {
        return option switch
        {
            "Draft" => IppAttributeValue.Single("print-quality", "3"),
            "Normal" => IppAttributeValue.Single("print-quality", "4"),
            "High" or "Photo" => IppAttributeValue.Single("print-quality", "5"),
            _ => null,
        };
    }

    private static string NormalizeKeyword(string keyword)
    {
        return string.Create(
            keyword.Length,
            keyword,
            static (buffer, source) =>
            {
                for (int index = 0; index < source.Length; index++)
                {
                    buffer[index] = source[index] == '_' ? '-' : char.ToLowerInvariant(source[index]);
                }
            });
    }
}
