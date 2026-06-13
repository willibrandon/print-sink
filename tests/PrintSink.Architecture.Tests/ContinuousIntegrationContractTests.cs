namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the repository CI contract for real print-stack validation.
/// </summary>
[TestClass]
internal sealed class ContinuousIntegrationContractTests
{
    /// <summary>
    /// Verifies CI builds, signs, installs, and exercises the packaged virtual printer.
    /// </summary>
    [TestMethod]
    public void WindowsCiRunsSignedPackageE2eOnSupportedPlatforms()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "windows-ci.yml");
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("platform: x64", workflow);
        Assert.Contains("platform: ARM64", workflow);
        Assert.Contains("Build signed MSIX", workflow);
        Assert.Contains("Real print-stack E2E", workflow);
        Assert.Contains("tests\\e2e\\Invoke-PrintSinkE2E.ps1", workflow);
        Assert.Contains("-PackagePath $package.FullName", workflow);
        Assert.Contains("-OutputDirectory", workflow);
        Assert.Contains("-Cleanup", workflow);

        AssertBefore(workflow, "Build", "Test");
        AssertBefore(workflow, "Test", "Packaged app tests");
        AssertBefore(workflow, "Packaged app tests", "Build signed MSIX");
        AssertBefore(workflow, "Build signed MSIX", "Real print-stack E2E");
    }

    /// <summary>
    /// Verifies E2E feature evidence must include concrete artifacts.
    /// </summary>
    [TestMethod]
    public void E2eFeatureEvidenceRequiresArtifacts()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string e2eScript = File.ReadAllText(e2ePath);

        Assert.Contains("[Parameter(Mandatory)]", e2eScript);
        Assert.Contains("[object] $Artifact", e2eScript);
        Assert.Contains("$null -eq $Artifact", e2eScript);
        Assert.Contains("$Artifact.Length -eq 0", e2eScript);
    }

    private static void AssertBefore(string text, string earlier, string later)
    {
        int earlierIndex = text.IndexOf(earlier, StringComparison.Ordinal);
        int laterIndex = text.IndexOf(later, StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, earlierIndex, $"Could not find '{earlier}'.");
        Assert.IsGreaterThanOrEqualTo(0, laterIndex, $"Could not find '{later}'.");
        Assert.IsLessThan(laterIndex, earlierIndex, $"'{earlier}' must appear before '{later}'.");
    }
}
