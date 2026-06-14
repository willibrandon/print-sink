using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests MSIX, WinUI, and WinRT activation packaging contracts.
/// </summary>
[TestClass]
internal sealed class PackagingContractTests
{
    private const string Uap10Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";

    private static readonly (string DisplayName, string PrinterUri, string PreferredInputFormat, string? OutputFileTypes, string PdcFile, string PdrFile, string[] SupportedFormats)[] ExpectedVirtualPrinters =
    [
        ("ms-resource:PdfPrintDisplayName", "printsink:print-to-pdf", "application/oxps", "pdf", "Config\\PrinterPdf.pdc.xml", "Config\\PrinterPdf.pdr.xml", ["application/pdf|1.7"]),
        ("ms-resource:XpsPrintDisplayName", "printsink:print-to-xps", "application/oxps", "xps;oxps", "Config\\PrinterXps.pdc.xml", "Config\\PrinterXps.pdr.xml", ["application/oxps|1.0", "application/vnd.ms-xpsdocument|1.0"]),
        ("ms-resource:PostScriptPrintDisplayName", "printsink:print-to-ps", "application/postscript", "ps", "Config\\PrinterPostScript.pdc.xml", "Config\\PrinterPostScript.pdr.xml", ["application/postscript|3.0"]),
        ("ms-resource:CloudPrintDisplayName", "printsink:print-to-cloud", "application/oxps", null, "Config\\PrinterCloud.pdc.xml", "Config\\PrinterCloud.pdr.xml", ["application/pdf|1.7"]),
        ("ms-resource:PwgRasterPrintDisplayName", "printsink:print-to-pwgr", "application/oxps", "pwgr", "Config\\PrinterPwgRaster.pdc.xml", "Config\\PrinterPwgRaster.pdr.xml", []),
        ("ms-resource:PclmPrintDisplayName", "printsink:print-to-pclm", "application/oxps", "pclm", "Config\\PrinterPclm.pdc.xml", "Config\\PrinterPclm.pdr.xml", []),
    ];

    /// <summary>
    /// Verifies the app project remains a packaged, self-contained WinUI MSIX host.
    /// </summary>
    [TestMethod]
    public void AppProjectRemainsSingleProjectMsixHost()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string appProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "PrintSink.App.csproj");
        XDocument appProject = XDocument.Load(appProjectPath);

        AssertProperty(appProject, "OutputType", "WinExe");
        AssertProperty(appProject, "TargetFramework", "$(PrintSinkWindowsTargetFramework)");
        AssertProperty(appProject, "RootNamespace", "PrintSink.App");
        AssertProperty(appProject, "UseWinUI", "true");
        AssertProperty(appProject, "WindowsPackageType", "MSIX");
        AssertProperty(appProject, "WindowsAppSDKSelfContained", "true");
        AssertProperty(appProject, "TargetPlatformMinVersion", "10.0.26100.0");
        AssertProperty(appProject, "SupportedOSPlatformVersion", "10.0.26100.0");
        AssertProperty(appProject, "RuntimeIdentifiers", "win-x64;win-arm64");
        AssertProperty(appProject, "EnableMsixTooling", "true");
        AssertProperty(appProject, "WinUISDKReferences", "false");
        AssertPackageReference(appProject, "Microsoft.WindowsAppSDK");
        AssertPackageReference(appProject, "Microsoft.UI.Reactor");
    }

    /// <summary>
    /// Verifies the background task project remains a CsWinRT component.
    /// </summary>
    [TestMethod]
    public void TasksProjectRemainsCsWinRtComponent()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string tasksProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.Tasks", "PrintSink.Tasks.csproj");
        XDocument tasksProject = XDocument.Load(tasksProjectPath);

        AssertProperty(tasksProject, "TargetFramework", "$(PrintSinkWindowsTargetFramework)");
        AssertProperty(tasksProject, "RootNamespace", "PrintSink.Tasks");
        AssertProperty(tasksProject, "Platforms", "x64;ARM64");
        AssertProperty(tasksProject, "RuntimeIdentifiers", "win-x64;win-arm64");
        AssertProperty(tasksProject, "UseWinUI", "true");
        AssertProperty(tasksProject, "CsWinRTComponent", "true");
        AssertProperty(tasksProject, "CsWinRTWindowsMetadata", "10.0.26100.0");
        AssertProperty(tasksProject, "GenerateDocumentationFile", "false");
        AssertPackageReference(tasksProject, "Microsoft.Windows.CsWinRT");
    }

    /// <summary>
    /// Verifies private projection projects stay scoped to the WinRT types PrintSink needs.
    /// </summary>
    [TestMethod]
    public void PrivateProjectionContractsStayScoped()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string appProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "PrintSink.App.csproj");
        string xpsProjectionPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps.Projections", "PrintSink.Xps.Projections.csproj");
        XDocument appProject = XDocument.Load(appProjectPath);
        XDocument xpsProjection = XDocument.Load(xpsProjectionPath);

        AssertProperty(appProject, "CsWinRTPrivateProjection", "true");
        AssertProperty(appProject, "CsWinRTWindowsMetadata", "10.0.26100.0");
        AssertPrivateProjectionIncludes(
            appProject,
            "Windows.Devices.Printers.VirtualPrinterManager",
            "Windows.Devices.Printers.IppAttributeConverter",
            "Windows.Devices.Printers.IPdlPassthroughProvider2");

        AssertProperty(xpsProjection, "CsWinRTPrivateProjection", "true");
        AssertProperty(xpsProjection, "CsWinRTWindowsMetadata", "10.0.26100.0");
        AssertPrivateProjectionIncludes(
            xpsProjection,
            "PrintSink.Xps.IXpsPageWatermarker",
            "PrintSink.Xps.XpsPageWatermarker",
            "PrintSink.Xps.XpsSequentialDocument");
    }

    /// <summary>
    /// Verifies the app package carries the WinRT activation and native XPS assets.
    /// </summary>
    [TestMethod]
    public void AppProjectPackagesActivationAndXpsAssets()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string appProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "PrintSink.App.csproj");
        string appProjectText = File.ReadAllText(appProjectPath);
        XDocument appProject = XDocument.Load(appProjectPath);

        AssertAppxPayload(appProject, "PrintSink.Tasks.winmd");
        AssertAppxPayload(appProject, "PrintSink.Xps.dll");
        AssertAppxPayload(appProject, "PrintSink.Xps.winmd");
        AssertAppxPayload(appProject, "PrintSink.Xps.dll.manifest");
        AssertAppxPayload(appProject, "PrintSink.Xps.Projections.dll");

        Assert.Contains("IncludePrintSinkTasksActivationAssets", appProjectText);
        Assert.Contains("WinRT.Host.dll", appProjectText);
        Assert.Contains("WinRT.Host.runtimeconfig.json", appProjectText);
        Assert.Contains("WinRT.Runtime.dll", appProjectText);
        Assert.Contains("IncludePrintSinkXpsAssets", appProjectText);
        Assert.Contains("PatchPrintSinkAppExecutionAliasManifest", appProjectText);
    }

    /// <summary>
    /// Verifies the source package manifest declares the full print-support extension surface.
    /// </summary>
    [TestMethod]
    public void PackageManifestDeclaresPrintSupportSurface()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Package.appxmanifest");
        XDocument manifest = XDocument.Load(manifestPath);

        AssertApplicationContract(manifest);
        AssertCapability(manifest, "privateNetworkClientServer");
        AssertCapability(manifest, "runFullTrust");
        AssertApplicationExtension(manifest, "windows.printSupportSettingsUI", "PrintSink.App.App");
        AssertApplicationExtension(manifest, "windows.printSupportJobUI", "PrintSink.App.App");
        AssertApplicationExtension(
            manifest,
            "windows.printSupportWorkflow",
            "PrintSink.Tasks.PrintSupportWorkflowBackgroundTask");
        AssertApplicationExtension(
            manifest,
            "windows.printSupportExtension",
            "PrintSink.Tasks.PrintSupportExtensionBackgroundTask");
        AssertExecutionAlias(manifest, "printsink-app.exe");
        AssertVirtualPrinterManifest(manifest, repositoryRoot);
        AssertInProcessServer(
            manifest,
            "WinRT.Host.dll",
            [
                "PrintSink.Tasks.PrintSupportWorkflowBackgroundTask",
                "PrintSink.Tasks.PrintSupportExtensionBackgroundTask",
                "PrintSink.Tasks.VirtualPrinterBackgroundTask",
            ]);
        AssertInProcessServer(
            manifest,
            "PrintSink.Xps.dll",
            [
                "PrintSink.Xps.XpsPageWatermarker",
                "PrintSink.Xps.XpsSequentialDocument",
            ]);
    }

    private static void AssertProperty(XDocument document, string name, string expectedValue)
    {
        string? actualValue = document
            .Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(static element => element.Value.Trim())
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        Assert.AreEqual(expectedValue, actualValue, $"{name} must be {expectedValue}.");
    }

    private static void AssertPackageReference(XDocument document, string packageId)
    {
        bool found = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Any(element => string.Equals((string?)element.Attribute("Include"), packageId, StringComparison.Ordinal));

        Assert.IsTrue(found, $"Expected PackageReference '{packageId}'.");
    }

    private static void AssertPrivateProjectionIncludes(XDocument document, params string[] expectedIncludes)
    {
        string includes = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "CsWinRTIncludesPrivate")
            .Select(static element => element.Value)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;

        foreach (string expectedInclude in expectedIncludes)
        {
            Assert.Contains(expectedInclude, includes);
        }
    }

    private static void AssertAppxPayload(XDocument document, string targetPath)
    {
        bool found = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "AppxPackagePayload")
            .Any(element => element
                .Elements()
                .Where(static child => child.Name.LocalName == "TargetPath")
                .Any(child => string.Equals(child.Value.Trim(), targetPath, StringComparison.Ordinal)));

        Assert.IsTrue(found, $"Expected AppxPackagePayload target '{targetPath}'.");
    }

    private static void AssertApplicationContract(XDocument document)
    {
        XElement application = AssertSingleElement(document, "Application", "application declaration");

        Assert.AreEqual("App", (string?)application.Attribute("Id"));
        Assert.AreEqual("$targetnametoken$.exe", (string?)application.Attribute("Executable"));
        Assert.AreEqual("$targetentrypoint$", (string?)application.Attribute("EntryPoint"));
        Assert.AreEqual(
            "true",
            (string?)application.Attribute(XName.Get("SupportsMultipleInstances", Uap10Namespace)));
    }

    private static void AssertExecutionAlias(XDocument document, string alias)
    {
        bool found = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "ExecutionAlias")
            .Any(element => string.Equals((string?)element.Attribute("Alias"), alias, StringComparison.Ordinal));

        Assert.IsTrue(found, $"Expected execution alias '{alias}'.");
    }

    private static void AssertApplicationExtension(XDocument document, string category, string entryPoint)
    {
        XElement extension = AssertSingleElement(
            document,
            "Extension",
            $"application extension '{category}'",
            element => string.Equals((string?)element.Attribute("Category"), category, StringComparison.Ordinal));

        Assert.AreEqual(entryPoint, (string?)extension.Attribute("EntryPoint"), $"{category} EntryPoint must match.");
    }

    private static void AssertCapability(XDocument document, string name)
    {
        bool found = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "Capability")
            .Any(element => string.Equals((string?)element.Attribute("Name"), name, StringComparison.Ordinal));

        Assert.IsTrue(found, $"Expected package capability '{name}'.");
    }

    private static void AssertVirtualPrinterManifest(XDocument document, string repositoryRoot)
    {
        XElement[] virtualPrinterExtensions =
        [
            .. document
                .Descendants()
                .Where(static element => element.Name.LocalName == "Extension")
                .Where(static element => string.Equals(
                    (string?)element.Attribute("Category"),
                    "windows.printSupportVirtualPrinterWorkflow",
                    StringComparison.Ordinal)),
        ];
        XElement[] virtualPrinters =
        [
            .. document
                .Descendants()
                .Where(static element => element.Name.LocalName == "PrintSupportVirtualPrinter"),
        ];

        Assert.HasCount(ExpectedVirtualPrinters.Length, virtualPrinterExtensions);
        Assert.HasCount(ExpectedVirtualPrinters.Length, virtualPrinters);

        foreach (XElement extension in virtualPrinterExtensions)
        {
            Assert.AreEqual(
                "PrintSink.Tasks.VirtualPrinterBackgroundTask",
                (string?)extension.Attribute("EntryPoint"),
                "Virtual-printer workflow extensions must activate the background task.");
            Assert.HasCount(
                1,
                extension
                    .Elements()
                    .Where(static element => element.Name.LocalName == "PrintSupportVirtualPrinter")
                    .ToArray());
        }

        foreach ((string displayName, string printerUri, string preferredInputFormat, string? outputFileTypes, string pdcFile, string pdrFile, string[] supportedFormats) in ExpectedVirtualPrinters)
        {
            XElement printer = virtualPrinters
                .Where(element => string.Equals((string?)element.Attribute("PrinterUri"), printerUri, StringComparison.Ordinal))
                .SingleOrDefault()
                ?? throw new AssertFailedException($"Expected virtual printer '{printerUri}'.");

            Assert.AreEqual(displayName, (string?)printer.Attribute("DisplayName"), $"{printerUri} DisplayName must match.");
            Assert.AreEqual(preferredInputFormat, (string?)printer.Attribute("PreferredInputFormat"), $"{printerUri} PreferredInputFormat must match.");
            Assert.AreEqual(pdcFile, (string?)printer.Attribute("PdcFile"), $"{printerUri} PdcFile must match.");
            Assert.AreEqual(pdrFile, (string?)printer.Attribute("PdrFile"), $"{printerUri} PdrFile must match.");

            XAttribute? actualOutputFileTypes = printer.Attribute("OutputFileTypes");
            if (outputFileTypes is null)
            {
                Assert.IsNull(actualOutputFileTypes, $"{printerUri} must not declare OutputFileTypes.");
            }
            else
            {
                Assert.AreEqual(outputFileTypes, actualOutputFileTypes?.Value, $"{printerUri} OutputFileTypes must match.");
            }

            CollectionAssert.AreEqual(
                supportedFormats,
                GetSupportedFormats(printer),
                $"{printerUri} supported formats must match.");
            AssertPackageAsset(repositoryRoot, pdcFile);
            AssertPackageAsset(repositoryRoot, pdrFile);
        }
    }

    private static string[] GetSupportedFormats(XElement printer)
    {
        return
        [
            .. printer
                .Descendants()
                .Where(static element => element.Name.LocalName == "SupportedFormat")
                .Select(static element => string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{(string?)element.Attribute("Type")}|{(string?)element.Attribute("MaxVersion")}"))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static void AssertPackageAsset(string repositoryRoot, string packageRelativePath)
    {
        string assetPath = Path.Combine(
            repositoryRoot,
            "src",
            "PrintSink.App",
            packageRelativePath.Replace('\\', Path.DirectorySeparatorChar));

        Assert.IsTrue(File.Exists(assetPath), $"Expected package asset '{packageRelativePath}'.");
    }

    private static void AssertInProcessServer(XDocument document, string path, string[] expectedClasses)
    {
        XElement server = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "InProcessServer")
            .SingleOrDefault(element => element
                .Elements()
                .Where(static child => child.Name.LocalName == "Path")
                .Any(child => string.Equals(child.Value.Trim(), path, StringComparison.Ordinal)))
            ?? throw new AssertFailedException($"Expected in-process server '{path}'.");

        string[] actualClasses =
        [
            .. server
                .Elements()
                .Where(static element => element.Name.LocalName == "ActivatableClass")
                .Select(static element => (string?)element.Attribute("ActivatableClassId"))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Order(StringComparer.Ordinal),
        ];

        CollectionAssert.AreEqual(
            expectedClasses.Order(StringComparer.Ordinal).ToArray(),
            actualClasses,
            $"{path} activatable classes must match.");

        foreach (XElement activationClass in server
            .Elements()
            .Where(static element => element.Name.LocalName == "ActivatableClass"))
        {
            Assert.AreEqual("both", (string?)activationClass.Attribute("ThreadingModel"), $"{path} activatable classes must be agile.");
        }
    }

    private static XElement AssertSingleElement(XDocument document, string localName, string description)
    {
        return AssertSingleElement(document, localName, description, static _ => true);
    }

    private static XElement AssertSingleElement(
        XDocument document,
        string localName,
        string description,
        Func<XElement, bool> predicate)
    {
        XElement[] matches =
        [
            .. document
                .Descendants()
                .Where(element => element.Name.LocalName == localName)
                .Where(predicate),
        ];

        Assert.HasCount(1, matches, $"Expected exactly one {description}.");
        return matches[0];
    }
}
