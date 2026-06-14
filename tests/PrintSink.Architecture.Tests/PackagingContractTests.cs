using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests MSIX, WinUI, and WinRT activation packaging contracts.
/// </summary>
[TestClass]
internal sealed class PackagingContractTests
{
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
}
