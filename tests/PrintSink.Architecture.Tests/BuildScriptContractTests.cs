namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the root build script contract.
/// </summary>
[TestClass]
internal sealed class BuildScriptContractTests
{
    /// <summary>
    /// Verifies the root build script uses the full MSBuild solution gate and propagates failures.
    /// </summary>
    [TestMethod]
    public void RootBuildScriptUsesMsbuildSolutionGate()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "build.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains(".\\PrintSink.slnx", script);
        Assert.Contains("/p:Configuration=$Configuration", script);
        Assert.Contains("/p:Platform=$Platform", script);
        Assert.Contains("/nologo", script);
        Assert.Contains("/v:minimal", script);
        Assert.Contains("$LASTEXITCODE -ne 0", script);
        Assert.Contains("exit $LASTEXITCODE", script);
        Assert.DoesNotContain("dotnet build", script);
    }

    /// <summary>
    /// Verifies the root E2E script builds and runs the signed package print-stack harness.
    /// </summary>
    [TestMethod]
    public void RootE2eScriptUsesSignedPackageHarness()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "test-e2e.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("tests\\e2e\\Invoke-PrintSinkE2E.ps1", script);
        Assert.Contains("GenerateAppxPackageOnBuild=true", script);
        Assert.Contains("AppxPackageSigningEnabled=true", script);
        Assert.Contains("PackageCertificateThumbprint", script);
        Assert.Contains("TrustedPeople", script);
        Assert.Contains("$KeepQueues", script);
        Assert.Contains("Cleanup = $true", script);
        Assert.Contains("$LASTEXITCODE -ne 0", script);
        Assert.DoesNotContain("StoreName]::Root", script);
    }
}
