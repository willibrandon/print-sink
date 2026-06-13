using System.Xml.Linq;
using PrintSink.Cli;
using PrintSink.Core.Capabilities;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tests.Commands;

/// <summary>
/// Tests validators against the package assets that ship with the app.
/// </summary>
[TestClass]
internal sealed class PackageAssetValidationTests
{
    /// <summary>
    /// Verifies the shipped package manifest matches the virtual-printer contract shape.
    /// </summary>
    [TestMethod]
    public void ManifestLintAcceptsShippedPackageManifest()
    {
        string repositoryRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Package.appxmanifest");

        ManifestLintResult result = ManifestLinter.Lint(manifestPath);

        Assert.IsTrue(result.Succeeded, string.Join(Environment.NewLine, result.Messages));
    }

    /// <summary>
    /// Verifies every shipped virtual-printer PDC file has a valid Print Schema shape.
    /// </summary>
    [TestMethod]
    public void PdcValidateAcceptsShippedVirtualPrinterCapabilities()
    {
        string repositoryRoot = FindRepositoryRoot();
        string configDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Config");
        string[] pdcFiles = [.. Directory
            .EnumerateFiles(configDirectory, "*.pdc.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)];

        Assert.HasCount(EndpointCatalog.All.Count, pdcFiles);

        List<string> failures = [];
        foreach (string pdcFile in pdcFiles)
        {
            ValidationResult result = PdcValidator.Validate(pdcFile);
            if (!result.Succeeded)
            {
                failures.Add($"{Path.GetFileName(pdcFile)}: {string.Join("; ", result.Messages)}");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies every shipped virtual-printer PDC file contains the custom feature nodes.
    /// </summary>
    [TestMethod]
    public void PdcFilesContainShippedCustomFeatureNodes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string configDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Config");
        string[] pdcFiles = [.. Directory
            .EnumerateFiles(configDirectory, "*.pdc.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)];
        string[] expectedLocalNames = GetExpectedCustomFeatureLocalNames();

        Assert.HasCount(EndpointCatalog.All.Count, pdcFiles);

        List<string> failures = [];
        foreach (string pdcFile in pdcFiles)
        {
            HashSet<string> customElementNames = XDocument
                .Load(pdcFile)
                .Descendants()
                .Where(static element => element.Name.NamespaceName == "https://schemas.printsink.dev/printing/keywords")
                .Select(static element => element.Name.LocalName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string expectedLocalName in expectedLocalNames)
            {
                if (!customElementNames.Contains(expectedLocalName))
                {
                    failures.Add($"{Path.GetFileName(pdcFile)} is missing '{expectedLocalName}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies every shipped virtual-printer PDR file contains the custom feature resource keys.
    /// </summary>
    [TestMethod]
    public void PdrFilesContainShippedCustomFeatureResources()
    {
        string repositoryRoot = FindRepositoryRoot();
        string configDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Config");
        string[] pdrFiles = [.. Directory
            .EnumerateFiles(configDirectory, "*.pdr.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)];
        string[] expectedResourceNames = GetExpectedCustomPdrResourceNames();

        Assert.HasCount(EndpointCatalog.All.Count, pdrFiles);

        List<string> failures = [];
        foreach (string pdrFile in pdrFiles)
        {
            HashSet<string> resourceNames = GetResxDataNames(pdrFile);
            foreach (string expectedResourceName in expectedResourceNames)
            {
                if (!resourceNames.Contains(expectedResourceName))
                {
                    failures.Add($"{Path.GetFileName(pdrFile)} is missing '{expectedResourceName}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies shipped RESW files cover manifest display names and custom feature display strings.
    /// </summary>
    [TestMethod]
    public void ReswFilesCoverManifestAndCustomFeatureResources()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App");
        HashSet<string> appResources = GetResxDataNames(Path.Combine(appDirectory, "Strings", "en-US", "Resources.resw"));
        HashSet<string> featureResources = GetResxDataNames(Path.Combine(appDirectory, "Strings", "en-US", "PrintSinkFeatures.resw"));
        string[] manifestResourceNames = GetManifestResourceNames(Path.Combine(appDirectory, "Package.appxmanifest"));
        string[] featureResourceNames = GetExpectedCustomFeatureLocalNames();

        List<string> failures = [];
        foreach (string resourceName in manifestResourceNames)
        {
            if (!appResources.Contains(resourceName))
            {
                failures.Add($"Resources.resw is missing '{resourceName}'.");
            }
        }

        foreach (string resourceName in featureResourceNames)
        {
            if (!featureResources.Contains(resourceName))
            {
                failures.Add($"PrintSinkFeatures.resw is missing '{resourceName}'.");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PrintSink.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate PrintSink.slnx.");
    }

    private static string[] GetManifestResourceNames(string manifestPath)
    {
        XDocument document = XDocument.Load(manifestPath);
        return [.. document
            .Descendants()
            .Attributes("DisplayName")
            .Select(attribute => attribute.Value)
            .Where(static value => value.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            .Select(static value => value["ms-resource:".Length..])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    private static HashSet<string> GetResxDataNames(string resourcePath)
    {
        XDocument document = XDocument.Load(resourcePath);
        return document
            .Descendants("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static string[] GetExpectedCustomFeatureLocalNames()
    {
        return [.. GetPrintSinkQualifiedNames()
            .Select(name => name.LocalName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    private static string[] GetExpectedCustomPdrResourceNames()
    {
        return [.. GetPrintSinkQualifiedNames()
            .Select(ToPdrResourceName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<PrintSchemaQualifiedName> GetPrintSinkQualifiedNames()
    {
        foreach (CustomFeature feature in PrintSinkCapabilityFeatures.BuiltIn)
        {
            if (IsPrintSinkName(feature.Name))
            {
                yield return feature.Name;
            }

            foreach (CustomFeatureOption option in feature.Options)
            {
                if (IsPrintSinkName(option.Name))
                {
                    yield return option.Name;
                }
            }
        }
    }

    private static bool IsPrintSinkName(PrintSchemaQualifiedName name)
    {
        return string.Equals(name.NamespaceUri.AbsoluteUri, "https://schemas.printsink.dev/printing/keywords", StringComparison.Ordinal);
    }

    private static string ToPdrResourceName(PrintSchemaQualifiedName name)
    {
        return string.Concat(name.NamespaceUri.AbsoluteUri["https://".Length..].TrimEnd('/'), "/", name.LocalName);
    }
}
