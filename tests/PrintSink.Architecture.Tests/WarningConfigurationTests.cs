using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests repository warning configuration rules.
/// </summary>
[TestClass]
internal sealed class WarningConfigurationTests
{
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

        List<string> failures = [];
        foreach (string projectFile in projectFiles)
        {
            XDocument document = XDocument.Load(projectFile);
            foreach (XElement noWarnElement in document.Descendants().Where(static element => element.Name.LocalName == "NoWarn"))
            {
                failures.Add(
                    $"{SourceFileDiscovery.RelativePath(repositoryRoot, projectFile)} declares NoWarn='{noWarnElement.Value}'.");
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
