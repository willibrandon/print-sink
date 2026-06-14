namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the code-first Microsoft.UI.Reactor foreground UI contract.
/// </summary>
[TestClass]
internal sealed class ReactorUiContractTests
{
    private static readonly string[] ReactorScreenFiles =
    [
        "AppRoot.cs",
        "ManagementScreen.cs",
        "SettingsScreen.cs",
        "JobPreviewScreen.cs",
        "WinRtPrintSourceScreen.cs",
    ];

    /// <summary>
    /// Verifies the packaged foreground app remains code-first Reactor UI instead of XAML pages.
    /// </summary>
    [TestMethod]
    public void AppProjectRemainsCodeFirstReactorUi()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string appProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "PrintSink.App.csproj");
        string appDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App");
        string appProject = File.ReadAllText(appProjectPath);
        string[] xamlFiles =
        [
            .. Directory
                .EnumerateFiles(appDirectory, "*.xaml", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(appDirectory, path))
                .OrderBy(static path => path, StringComparer.Ordinal),
        ];

        Assert.Contains("<UseWinUI>true</UseWinUI>", appProject);
        Assert.Contains("<WindowsPackageType>MSIX</WindowsPackageType>", appProject);
        Assert.Contains("<PackageReference Include=\"Microsoft.UI.Reactor\" />", appProject);
        Assert.DoesNotContain("<ApplicationDefinition", appProject);
        Assert.DoesNotContain("<Page", appProject);
        Assert.IsEmpty(xamlFiles, $"PrintSink.App must stay code-first Reactor UI. XAML files: {string.Join(", ", xamlFiles)}");
    }

    /// <summary>
    /// Verifies foreground activation routes are rendered through Reactor components.
    /// </summary>
    [TestMethod]
    public void ForegroundActivationRoutesUseReactorComponents()
    {
        string app = ReadAppFile("App.cs");
        string appRoot = ReadAppFile("AppRoot.cs");

        Assert.Contains("using Microsoft.UI.Reactor;", app);
        Assert.Contains("ReactorApp.Run<AppRoot>", app);
        Assert.DoesNotContain("InitializeComponent", app);

        Assert.Contains("internal sealed class AppRoot : Component", appRoot);
        Assert.Contains("AppActivationRouteKind.Settings => Component<SettingsScreen, AppActivationRoute>(route)", appRoot);
        Assert.Contains("AppActivationRouteKind.JobPreview => Component<JobPreviewScreen, AppActivationRoute>(route)", appRoot);
        Assert.Contains("AppActivationRouteKind.WinRtPrintSource => Component<WinRtPrintSourceScreen, AppActivationRoute>(route)", appRoot);
        Assert.Contains("_ => Component<ManagementScreen>()", appRoot);
        Assert.DoesNotContain("new MainWindow", appRoot);

        foreach (string screenFile in ReactorScreenFiles)
        {
            string screen = ReadAppFile(screenFile);

            Assert.Contains("using Microsoft.UI.Reactor;", screen);
            Assert.Contains("public override Element Render()", screen);
            Assert.DoesNotContain("InitializeComponent", screen);
            Assert.DoesNotContain(": Page", screen);
            Assert.DoesNotContain(": Window", screen);
        }
    }

    /// <summary>
    /// Verifies the design and testing docs keep the Reactor UI model explicit.
    /// </summary>
    [TestMethod]
    public void DocumentationKeepsReactorUiModelExplicit()
    {
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("**Microsoft.UI.Reactor** for the foreground UI", design);
        Assert.Contains("code-first WinUI: no XAML pages", design);
        Assert.Contains("no code-behind layer", design);
        Assert.Contains("Reactor screen behavior", design);
        Assert.Contains("absence of Reactor render", testing);
    }

    private static string ReadAppFile(string fileName)
    {
        return ReadRepositoryFile("src", "PrintSink.App", fileName);
    }

    private static string ReadRepositoryFile(params string[] relativePathParts)
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string path = Path.Combine([repositoryRoot, .. relativePathParts]);
        return File.ReadAllText(path);
    }
}
