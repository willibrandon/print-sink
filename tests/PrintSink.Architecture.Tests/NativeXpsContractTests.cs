using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the native XPS component contract that backs real watermarking and XPS streaming.
/// </summary>
[TestClass]
internal sealed class NativeXpsContractTests
{
    private static readonly string[] ExpectedNativeXpsActivatableClasses =
    [
        "PrintSink.Xps.XpsPageWatermarker",
        "PrintSink.Xps.XpsSequentialDocument",
    ];

    /// <summary>
    /// Verifies the native project remains a C++/WinRT Windows Runtime component.
    /// </summary>
    [TestMethod]
    public void NativeXpsProjectRemainsCppWinRtComponent()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps", "PrintSink.Xps.vcxproj");
        XDocument project = XDocument.Load(projectPath);

        AssertProperty(project, "VCProjectVersion", "18.0");
        AssertProperty(project, "Keyword", "WindowsRuntimeComponent");
        AssertProperty(project, "RootNamespace", "PrintSink.Xps");
        AssertProperty(project, "ProjectName", "PrintSink.Xps");
        AssertProperty(project, "TargetName", "PrintSink.Xps");
        AssertProperty(project, "MinimumVisualStudioVersion", "18.0");
        AssertProperty(project, "WindowsTargetPlatformVersion", "10.0.26100.0");
        AssertProperty(project, "WindowsTargetPlatformMinVersion", "10.0.26100.0");
        AssertProperty(project, "AppContainerApplication", "false");
        AssertProperty(project, "WindowsPackageType", "None");
        AssertProperty(project, "CppWinRTOptimized", "true");
        AssertProperty(project, "CppWinRTRootNamespaceAutoMerge", "true");
        AssertProperty(project, "CppWinRTGenerateWindowsMetadata", "true");
        AssertProperty(project, "RestoreProjectStyle", "PackageReference");
        AssertProperty(project, "ConfigurationType", "DynamicLibrary");
        AssertProperty(project, "PlatformToolset", "v145");
        AssertProperty(project, "DesktopCompatible", "true");
        AssertProperty(project, "ModuleDefinitionFile", "PrintSink.Xps.def");

        AssertPackageReference(project, "Microsoft.Windows.CppWinRT");
        AssertItemInclude(project, "Midl", "XpsPageWatermarker.idl");
        AssertItemInclude(project, "None", "PrintSink.Xps.def");
        AssertItemInclude(project, "None", "PrintSink.Xps.dll.manifest");
        AssertItemInclude(project, "ClCompile", "$(GeneratedFilesDir)module.g.cpp");
    }

    /// <summary>
    /// Verifies the native component declares the WinRT classes consumed from managed code.
    /// </summary>
    [TestMethod]
    public void NativeXpsManifestDeclaresRequiredActivatableClasses()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps", "PrintSink.Xps.dll.manifest");
        XDocument manifest = XDocument.Load(manifestPath);
        XNamespace assemblyNamespace = "urn:schemas-microsoft-com:asm.v1";
        XNamespace winRtNamespace = "urn:schemas-microsoft-com:winrt.v1";

        XElement assemblyIdentity = manifest
            .Descendants(assemblyNamespace + "assemblyIdentity")
            .Single();
        Assert.AreEqual("1.0.0.0", (string?)assemblyIdentity.Attribute("version"));
        Assert.AreEqual("PrintSink.Xps.dll", (string?)assemblyIdentity.Attribute("name"));
        Assert.AreEqual("win32", (string?)assemblyIdentity.Attribute("type"));

        XElement file = manifest
            .Descendants(assemblyNamespace + "file")
            .Single();
        Assert.AreEqual("PrintSink.Xps.dll", (string?)file.Attribute("name"));

        string[] activatableClasses =
        [
            .. manifest
                .Descendants(winRtNamespace + "activatableClass")
                .Select(static element => (string?)element.Attribute("name"))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Order(StringComparer.Ordinal),
        ];

        CollectionAssert.AreEqual(
            ExpectedNativeXpsActivatableClasses,
            activatableClasses);

        foreach (XElement activatableClass in manifest.Descendants(winRtNamespace + "activatableClass"))
        {
            Assert.AreEqual("both", (string?)activatableClass.Attribute("threadingModel"));
        }
    }

    /// <summary>
    /// Verifies the native component exports the standard C++/WinRT activation entry points.
    /// </summary>
    [TestMethod]
    public void NativeXpsDefinitionExportsWinRtActivationEntryPoints()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string definitionPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps", "PrintSink.Xps.def");
        string definition = File.ReadAllText(definitionPath);

        Assert.Contains("DllCanUnloadNow = WINRT_CanUnloadNow", definition);
        Assert.Contains("DllGetActivationFactory = WINRT_GetActivationFactory", definition);
        Assert.Contains("PRIVATE", definition);
    }

    /// <summary>
    /// Verifies the IDL keeps both native runtime classes and their managed projection surface.
    /// </summary>
    [TestMethod]
    public void NativeXpsIdlDeclaresWatermarkerAndSequentialDocument()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string idlPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps", "XpsPageWatermarker.idl");
        string idl = File.ReadAllText(idlPath);

        Assert.Contains("runtimeclass XpsPageWatermarker", idl);
        Assert.Contains("Windows.Storage.Streams.IRandomAccessStream ApplyToPackage", idl);
        Assert.Contains("runtimeclass XpsSequentialDocument", idl);
        Assert.Contains("Windows.Storage.Streams.IInputStream GetWatermarkedStream", idl);
        Assert.Contains("event Windows.Foundation.TypedEventHandler<XpsSequentialDocument, UInt64> XpsGenerationFailed", idl);
    }

    /// <summary>
    /// Verifies dotnet CLI builds cannot silently pretend the native project was built.
    /// </summary>
    [TestMethod]
    public void NativeXpsDotNetFallbackOnlyExplainsFullBuildRequirement()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string targetsPath = Path.Combine(repositoryRoot, "src", "PrintSink.Xps", "PrintSink.Xps.DotNet.targets");
        XDocument targets = XDocument.Load(targetsPath);
        string targetsText = File.ReadAllText(targetsPath);

        Assert.Contains("Skipping PrintSink.Xps because VC targets are unavailable.", targetsText);
        Assert.Contains("Use MSBuild.exe or Visual Studio for the full native solution build.", targetsText);
        AssertTargetExists(targets, "Build");
        AssertTargetExists(targets, "Rebuild");
        AssertTargetExists(targets, "Clean");
        AssertTargetExists(targets, "VSTest");
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

    private static void AssertItemInclude(XDocument document, string itemName, string includeValue)
    {
        bool found = document
            .Descendants()
            .Where(element => element.Name.LocalName == itemName)
            .Any(element => string.Equals((string?)element.Attribute("Include"), includeValue, StringComparison.Ordinal));

        Assert.IsTrue(found, $"Expected {itemName} include '{includeValue}'.");
    }

    private static void AssertTargetExists(XDocument document, string targetName)
    {
        bool found = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "Target")
            .Any(element => string.Equals((string?)element.Attribute("Name"), targetName, StringComparison.Ordinal));

        Assert.IsTrue(found, $"Expected target '{targetName}'.");
    }
}
