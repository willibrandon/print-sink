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
        Assert.Contains("Find-PrintSinkPackageCertificate", script);
        Assert.Contains("HasPrivateKey", script);
        Assert.Contains("NotAfter", script);
        Assert.Contains("TrustedPeople", script);
        Assert.Contains("Assert-PrintSinkE2EResult.ps1", script);
        Assert.Contains("$resultPath = Join-Path $OutputDirectory 'e2e-result.json'", script);
        Assert.Contains("ResultPath = $resultPath", script);
        Assert.Contains("$resultAssertionParameters.RequireCleanup = $true", script);
        Assert.Contains("$KeepQueues", script);
        Assert.Contains("$KeepPackage", script);
        Assert.Contains("Cleanup = $true", script);
        Assert.Contains("Remove-PrintSinkPackage -ResultPath $resultPath", script);
        Assert.Contains("$LASTEXITCODE -ne 0", script);
        Assert.Contains("$ProgressPreference = 'SilentlyContinue'", script);
        Assert.DoesNotContain("StoreName]::Root", script);
    }

    /// <summary>
    /// Verifies the packaged app test script removes its registered test package by default.
    /// </summary>
    [TestMethod]
    public void PackagedAppTestScriptCleansRegisteredTestPackage()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "test-app.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("[switch] $KeepPackage", script);
        Assert.Contains("function Remove-PackagedAppTestPackage", script);
        Assert.Contains("Get-AppxPackage -Name 'PrintSink.App.Tests'", script);
        Assert.Contains("Remove-AppxPackage -Package $package.PackageFullName", script);
        Assert.Contains("if (-not $KeepPackage)", script);
        Assert.Contains("exit $testExitCode", script);
        Assert.Contains("$ProgressPreference = 'SilentlyContinue'", script);
    }
}
