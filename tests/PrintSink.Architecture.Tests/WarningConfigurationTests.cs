using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests repository warning configuration rules.
/// </summary>
[TestClass]
internal sealed class WarningConfigurationTests
{
    private static readonly string PragmaWarningDisable = string.Concat("#pragma warning ", "disable");
    private static readonly string SuppressionAttributeSearch = string.Concat("Suppress", "MessageAttribute");
    private static readonly string SuppressionUsageSearch = string.Concat("[Suppress", "Message");

    /// <summary>
    /// Verifies the shared build configuration enables the expected analyzer gate.
    /// </summary>
    [TestMethod]
    public void SharedBuildConfigurationEnforcesLatestAllAnalyzers()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string propsPath = Path.Combine(repositoryRoot, "Directory.Build.props");
        XDocument document = XDocument.Load(propsPath);

        AssertProperty(document, "TreatWarningsAsErrors", "true");
        AssertProperty(document, "AnalysisLevel", "latest-all");
        AssertProperty(document, "EnforceCodeStyleInBuild", "true");
    }

    /// <summary>
    /// Verifies repository projects do not suppress compiler or analyzer warnings.
    /// </summary>
    [TestMethod]
    public void ProjectFilesDoNotDisableWarnings()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] projectFiles = SourceFileDiscovery.EnumerateRepositoryBuildFiles(repositoryRoot);
        string[] forbiddenProperties =
        [
            "NoWarn",
            "WarningsNotAsErrors",
            "WarningsAsMessages",
            "CodeAnalysisTreatWarningsAsErrors",
        ];

        List<string> failures = [];
        foreach (string projectFile in projectFiles)
        {
            XDocument document = XDocument.Load(projectFile);
            foreach (XElement suppressionElement in document
                .Descendants()
                .Where(element => forbiddenProperties.Contains(element.Name.LocalName, StringComparer.Ordinal)))
            {
                failures.Add(
                    $"{SourceFileDiscovery.RelativePath(repositoryRoot, projectFile)} declares {suppressionElement.Name.LocalName}='{suppressionElement.Value}'.");
            }

            foreach (XElement treatWarningsAsErrorsElement in document
                .Descendants()
                .Where(static element => element.Name.LocalName == "TreatWarningsAsErrors"))
            {
                if (!string.Equals(treatWarningsAsErrorsElement.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"{SourceFileDiscovery.RelativePath(repositoryRoot, projectFile)} declares TreatWarningsAsErrors='{treatWarningsAsErrorsElement.Value}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies repository source files do not suppress compiler or analyzer warnings.
    /// </summary>
    [TestMethod]
    public void SourceFilesDoNotSuppressWarnings()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);
            if (source.Contains(PragmaWarningDisable, StringComparison.Ordinal))
            {
                failures.Add($"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} disables warnings with #pragma.");
            }

            if (source.Contains(SuppressionAttributeSearch, StringComparison.Ordinal)
                || source.Contains(SuppressionUsageSearch, StringComparison.Ordinal))
            {
                failures.Add($"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} suppresses warnings with a source-level attribute.");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    private static void AssertProperty(XDocument document, string name, string expectedValue)
    {
        string? actualValue = document
            .Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(static element => element.Value)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        Assert.AreEqual(expectedValue, actualValue, $"{name} must be {expectedValue}.");
    }
}
