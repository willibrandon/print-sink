using System.Text.RegularExpressions;
using System.Xml.Linq;
using PrintSink.Endpoints;
using PrintSink.Pdl;

namespace PrintSink.Core.Tests.Architecture;

/// <summary>
/// Tests package manifest contracts that must stay aligned with the core endpoint catalog.
/// </summary>
[TestClass]
internal sealed partial class ManifestContractTests
{
    private static readonly XNamespace PrintSupport = "http://schemas.microsoft.com/appx/manifest/printsupport/windows10";
    private static readonly XNamespace PrintSupport2 = "http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2";

    /// <summary>
    /// Verifies the package declares the same built-in virtual printers as the endpoint catalog.
    /// </summary>
    [TestMethod]
    public void VirtualPrinterManifestMatchesEndpointCatalog()
    {
        string appRoot = Path.Combine(FindRepositoryRoot(), "src", "PrintSink.App");
        XDocument manifest = XDocument.Load(Path.Combine(appRoot, "Package.appxmanifest"));
        XElement[] printers = manifest.Descendants(PrintSupport2 + "PrintSupportVirtualPrinter").ToArray();

        Assert.AreEqual(EndpointCatalog.BuiltInQueues.Count, printers.Length);

        Dictionary<string, XElement> printersByUri = printers.ToDictionary(
            printer => RequiredAttribute(printer, "PrinterUri"),
            StringComparer.Ordinal);

        foreach (VirtualEndpoint endpoint in EndpointCatalog.BuiltInQueues)
        {
            string printerUri = "printsink:" + endpoint.EndpointPath;
            Assert.IsTrue(printersByUri.TryGetValue(printerUri, out XElement? printer), $"Missing printer URI '{printerUri}'.");
            AssertPrinterMatchesEndpoint(appRoot, printer, endpoint);
        }
    }

    /// <summary>
    /// Verifies all manifest queue display-name resources are present.
    /// </summary>
    [TestMethod]
    public void ManifestDisplayNameResourcesExist()
    {
        string appRoot = Path.Combine(FindRepositoryRoot(), "src", "PrintSink.App");
        XDocument resources = XDocument.Load(Path.Combine(appRoot, "Strings", "en-US", "Resources.resw"));
        HashSet<string> resourceKeys = resources.Descendants("data")
            .Select(element => RequiredAttribute(element, "name"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (VirtualEndpoint endpoint in EndpointCatalog.BuiltInQueues)
        {
            Assert.IsTrue(resourceKeys.Contains(endpoint.QueueResourceName), $"Missing string resource '{endpoint.QueueResourceName}'.");
        }
    }

    /// <summary>
    /// Verifies foreground print-support UI contracts activate the Reactor application class.
    /// </summary>
    [TestMethod]
    public void ForegroundPrintSupportContractsUseReactorApplication()
    {
        string appRoot = Path.Combine(FindRepositoryRoot(), "src", "PrintSink.App");
        XDocument manifest = XDocument.Load(Path.Combine(appRoot, "Package.appxmanifest"));
        Dictionary<string, XElement> extensionsByCategory = manifest.Descendants(PrintSupport + "Extension")
            .ToDictionary(extension => RequiredAttribute(extension, "Category"), StringComparer.Ordinal);

        AssertForegroundEntryPoint(extensionsByCategory, "windows.printSupportSettingsUI");
        AssertForegroundEntryPoint(extensionsByCategory, "windows.printSupportJobUI");
    }

    private static void AssertPrinterMatchesEndpoint(string appRoot, XElement printer, VirtualEndpoint endpoint)
    {
        Assert.AreEqual("ms-resource:" + endpoint.QueueResourceName, RequiredAttribute(printer, "DisplayName"));
        Assert.AreEqual(PdlFormatInfo.ToContentType(endpoint.PreferredInputFormat), RequiredAttribute(printer, "PreferredInputFormat"));

        AssertOutputFileTypes(printer, endpoint);
        AssertSupportedFormats(printer, endpoint);
        AssertPackageXmlFile(appRoot, RequiredAttribute(printer, "PdcFile"), "PrintDeviceCapabilities");
        AssertPackageXmlFile(appRoot, RequiredAttribute(printer, "PdrFile"), "root");
    }

    private static void AssertOutputFileTypes(XElement printer, VirtualEndpoint endpoint)
    {
        XAttribute? outputFileTypes = printer.Attribute("OutputFileTypes");
        if (!endpoint.UsesSaveAsDialog)
        {
            Assert.IsNull(outputFileTypes);
            return;
        }

        Assert.IsNotNull(outputFileTypes);
        Assert.IsFalse(outputFileTypes.Value.Contains(';', StringComparison.Ordinal), "OutputFileTypes must use the current comma-delimited schema form.");

        string[] actualExtensions = outputFileTypes.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] expectedExtensions = endpoint.OutputFileExtensions
            .Select(extension => extension.TrimStart('.'))
            .ToArray();

        CollectionAssert.AreEqual(expectedExtensions, actualExtensions);
    }

    private static void AssertSupportedFormats(XElement printer, VirtualEndpoint endpoint)
    {
        XElement supportedFormats = printer.Element(PrintSupport2 + "SupportedFormats")
            ?? throw new AssertFailedException("Missing SupportedFormats element.");

        XElement[] supportedFormatElements = supportedFormats.Elements(PrintSupport2 + "SupportedFormat").ToArray();
        string[] actualTypes = supportedFormatElements
            .Select(element => RequiredAttribute(element, "Type"))
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();
        string[] expectedTypes = endpoint.SupportedPassthroughFormats
            .Select(PdlFormatInfo.ToContentType)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expectedTypes, actualTypes);

        foreach (XElement supportedFormat in supportedFormatElements)
        {
            XAttribute? maxVersion = supportedFormat.Attribute("MaxVersion");
            if (maxVersion is not null)
            {
                Assert.IsTrue(MaxVersionRegex().IsMatch(maxVersion.Value), $"Invalid MaxVersion '{maxVersion.Value}'.");
            }
        }
    }

    private static void AssertPackageXmlFile(string appRoot, string packagePath, string expectedRootLocalName)
    {
        string filePath = ResolvePackageFile(appRoot, packagePath);
        Assert.IsTrue(File.Exists(filePath), $"Missing package file '{packagePath}'.");

        XDocument document = XDocument.Load(filePath);
        Assert.AreEqual(expectedRootLocalName, document.Root?.Name.LocalName);
    }

    private static void AssertForegroundEntryPoint(Dictionary<string, XElement> extensionsByCategory, string category)
    {
        Assert.IsTrue(extensionsByCategory.TryGetValue(category, out XElement? extension), $"Missing extension '{category}'.");
        Assert.AreEqual("Microsoft.UI.Reactor.ReactorApplication", RequiredAttribute(extension, "EntryPoint"));
    }

    private static string ResolvePackageFile(string appRoot, string packagePath)
    {
        string[] pathParts = packagePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine([appRoot, .. pathParts]);
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        return element.Attribute(name)?.Value
            ?? throw new AssertFailedException($"Missing required attribute '{name}' on '{element.Name.LocalName}'.");
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

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    [GeneratedRegex(@"^\d+\.\d+$", RegexOptions.Compiled)]
    private static partial Regex MaxVersionRegex();
}
