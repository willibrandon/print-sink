using System.Xml;
using System.Xml.Linq;
using PrintSink.Core.Capabilities;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;

namespace PrintSink.Cli;

/// <summary>
/// Validates the package manifest shape needed by PrintSink.
/// </summary>
internal static class ManifestLinter
{
    private static readonly string[] RequiredExtensionCategories =
    [
        "windows.printSupportVirtualPrinterWorkflow",
        "windows.printSupportWorkflow",
        "windows.printSupportExtension",
        "windows.printSupportSettingsUI",
        "windows.printSupportJobUI",
    ];

    /// <summary>
    /// Lints an MSIX package manifest.
    /// </summary>
    /// <param name="manifestPath">The manifest path.</param>
    /// <returns>The lint result.</returns>
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

        ValidateVirtualPrinters(package, Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, messages);

        if (messages.Count == 0)
        {
            messages.Add("ok: manifest package shape is valid.");
        }

        return new ManifestLintResult(messages.All(message => !message.StartsWith("error:", StringComparison.Ordinal)), messages);
    }

    private static void ValidateVirtualPrinters(XElement package, string manifestDirectory, List<string> messages)
    {
        List<XElement> printerElements = package
            .Descendants()
            .Where(element => element.Name.LocalName == "PrintSupportVirtualPrinter")
            .ToList();

        if (printerElements.Count == 0)
        {
            messages.Add("error: at least one PrintSupportVirtualPrinter declaration is required.");
            return;
        }

        HashSet<string> declaredUris = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement printerElement in printerElements)
        {
            ValidateVirtualPrinter(printerElement, manifestDirectory, declaredUris, messages);
        }

        foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
        {
            if (!declaredUris.Contains(endpoint.PrinterUri.AbsoluteUri))
            {
                messages.Add($"error: missing virtual printer for endpoint '{endpoint.QueueName}' ({endpoint.PrinterUri}).");
            }
        }
    }

    private static void ValidateVirtualPrinter(
        XElement printerElement,
        string manifestDirectory,
        HashSet<string> declaredUris,
        List<string> messages)
    {
        string? printerUriText = (string?)printerElement.Attribute("PrinterUri");
        if (string.IsNullOrWhiteSpace(printerUriText) || !Uri.TryCreate(printerUriText, UriKind.Absolute, out Uri? printerUri))
        {
            messages.Add("error: virtual printer PrinterUri must be an absolute URI.");
            return;
        }

        if (!declaredUris.Add(printerUri.AbsoluteUri))
        {
            messages.Add($"error: duplicate virtual printer PrinterUri '{printerUri}'.");
        }

        if (!EndpointCatalog.TryResolve(printerUri, out VirtualEndpoint? endpoint))
        {
            messages.Add($"error: virtual printer '{printerUri}' is not registered in EndpointCatalog.");
            return;
        }

        VirtualEndpoint resolvedEndpoint = endpoint ?? throw new InvalidOperationException("Endpoint resolution returned null after success.");
        AddRequiredAttributeMessage(printerElement, "DisplayName", $"display name for '{resolvedEndpoint.QueueName}'", messages);
        ValidatePreferredInputFormat(printerElement, resolvedEndpoint, messages);
        ValidateOutputFileTypes(printerElement, resolvedEndpoint, messages);
        ValidateSupportedFormats(printerElement, resolvedEndpoint, messages);
        ValidatePackageXmlResource(printerElement, "PdcFile", manifestDirectory, true, messages);
        ValidatePackageXmlResource(printerElement, "PdrFile", manifestDirectory, false, messages);
    }

    private static void ValidatePreferredInputFormat(XElement printerElement, VirtualEndpoint endpoint, List<string> messages)
    {
        string? preferredInputFormat = (string?)printerElement.Attribute("PreferredInputFormat");
        if (string.IsNullOrWhiteSpace(preferredInputFormat))
        {
            preferredInputFormat = PdlFormatInfo.OxpsContentType;
        }

        if (preferredInputFormat is not PdlFormatInfo.OxpsContentType and not PdlFormatInfo.PostScriptContentType)
        {
            messages.Add($"error: '{endpoint.QueueName}' PreferredInputFormat must be application/oxps or application/postscript.");
            return;
        }

        if (!PdlFormatInfo.TryParseContentType(preferredInputFormat, out PdlFormat preferredFormat)
            || preferredFormat != endpoint.PreferredInputFormat)
        {
            messages.Add($"error: '{endpoint.QueueName}' PreferredInputFormat must be {PdlFormatInfo.GetContentType(endpoint.PreferredInputFormat)}.");
        }
    }

    private static void ValidateOutputFileTypes(XElement printerElement, VirtualEndpoint endpoint, List<string> messages)
    {
        string? outputFileTypes = (string?)printerElement.Attribute("OutputFileTypes");
        if (!endpoint.RequiresTargetFile)
        {
            if (!string.IsNullOrWhiteSpace(outputFileTypes))
            {
                messages.Add($"error: '{endpoint.QueueName}' must omit OutputFileTypes because it is not a file-backed sink.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(outputFileTypes))
        {
            messages.Add($"error: '{endpoint.QueueName}' must declare OutputFileTypes.");
            return;
        }

        string expectedExtension = endpoint.DefaultExtension?.TrimStart('.') ?? string.Empty;
        HashSet<string> declaredExtensions = SplitDelimitedValues(outputFileTypes)
            .Select(extension => extension.TrimStart('.'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!declaredExtensions.Contains(expectedExtension))
        {
            messages.Add($"error: '{endpoint.QueueName}' OutputFileTypes must include '{expectedExtension}'.");
        }
    }

    private static void ValidateSupportedFormats(XElement printerElement, VirtualEndpoint endpoint, List<string> messages)
    {
        Dictionary<PdlFormat, XElement> declaredFormats = printerElement
            .Descendants()
            .Where(element => element.Name.LocalName == "SupportedFormat")
            .Select(element => ((string?)element.Attribute("Type"), Element: element))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Item1))
            .Select(pair => (Parsed: PdlFormatInfo.TryParseContentType(pair.Item1!, out PdlFormat format), Format: format, pair.Element))
            .Where(pair => pair.Parsed)
            .GroupBy(pair => pair.Format)
            .ToDictionary(group => group.Key, group => group.First().Element);

        foreach (XElement supportedFormat in printerElement.Descendants().Where(element => element.Name.LocalName == "SupportedFormat"))
        {
            string? type = (string?)supportedFormat.Attribute("Type");
            if (string.IsNullOrWhiteSpace(type) || !PdlFormatInfo.TryParseContentType(type, out _))
            {
                messages.Add($"error: '{endpoint.QueueName}' has an unsupported SupportedFormat Type '{type}'.");
            }

            string? maxVersion = (string?)supportedFormat.Attribute("MaxVersion");
            if (!string.IsNullOrWhiteSpace(maxVersion) && !IsMajorMinorVersion(maxVersion))
            {
                messages.Add($"error: '{endpoint.QueueName}' SupportedFormat MaxVersion '{maxVersion}' must use Major.Minor digits.");
            }
        }

        foreach (PdlFormat passthroughFormat in endpoint.PassthroughFormats)
        {
            if (!declaredFormats.ContainsKey(passthroughFormat))
            {
                messages.Add($"error: '{endpoint.QueueName}' SupportedFormats must include {PdlFormatInfo.GetContentType(passthroughFormat)}.");
            }
        }
    }

    private static void ValidatePackageXmlResource(
        XElement printerElement,
        string attributeName,
        string manifestDirectory,
        bool required,
        List<string> messages)
    {
        string? value = (string?)printerElement.Attribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                messages.Add($"error: virtual printer {attributeName} is required.");
            }

            return;
        }

        string path = ResolvePackageResourcePath(manifestDirectory, value);
        if (!File.Exists(path))
        {
            messages.Add($"error: virtual printer {attributeName} file not found: {value}");
            return;
        }

        XDocument document;
        try
        {
            document = XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            messages.Add($"error: virtual printer {attributeName} XML is invalid: {ex.Message}");
            return;
        }

        if (attributeName == "PdcFile")
        {
            foreach (string validationMessage in PrintDeviceCapabilitiesValidator.Validate(document))
            {
                messages.Add($"error: virtual printer {attributeName} is invalid: {validationMessage}");
            }
        }
    }

    private static string ResolvePackageResourcePath(string manifestDirectory, string value)
    {
        string relativePath = value.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase)
            ? value["ms-appx:///".Length..]
            : value;

        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        return Path.Combine(manifestDirectory, relativePath);
    }

    private static IEnumerable<string> SplitDelimitedValues(string value)
    {
        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsMajorMinorVersion(string value)
    {
        string[] parts = value.Split('.');
        return parts.Length == 2
            && int.TryParse(parts[0], out _)
            && int.TryParse(parts[1], out _);
    }

    private static void AddRequiredAttributeMessage(
        XElement? element,
        string attributeName,
        string displayName,
        List<string> messages)
    {
        if (string.IsNullOrWhiteSpace((string?)element?.Attribute(attributeName)))
        {
            messages.Add($"error: {displayName} is required.");
        }
    }
}
