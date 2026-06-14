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
        Assert.Contains("Real print-stack E2E", workflow);
        Assert.Contains(".\\test-e2e.ps1", workflow);
        Assert.Contains("-BuildPackage", workflow);
        Assert.Contains("-Platform ${{ matrix.platform }}", workflow);
        Assert.DoesNotContain("tests\\e2e\\Invoke-PrintSinkE2E.ps1", workflow);
        Assert.DoesNotContain("New-SelfSignedCertificate", workflow);
        Assert.DoesNotContain("PackageCertificateThumbprint", workflow);
        Assert.IsFalse(
            workflow.Contains("StoreName]::Root", StringComparison.Ordinal),
            "CI package trust must not write to a Root store.");

        AssertBefore(workflow, "Build", "Test");
        AssertBefore(workflow, "Test", "Packaged app tests");
        AssertBefore(workflow, "Packaged app tests", "Core coverage");
        AssertBefore(workflow, "Core coverage", "Real print-stack E2E");
    }

    /// <summary>
    /// Verifies the real print-stack E2E workflow step cannot be made advisory.
    /// </summary>
    [TestMethod]
    public void WindowsCiDoesNotMakeRealPrintStackE2eOptional()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "windows-ci.yml");
        string workflow = File.ReadAllText(workflowPath);
        string e2eStep = ExtractScriptBlock(
            workflow,
            "- name: Real print-stack E2E",
            "- name: Upload test results");

        Assert.DoesNotContain("continue-on-error", e2eStep);
        Assert.DoesNotContain("if: always()", e2eStep);
        Assert.DoesNotContain("|| true", e2eStep);
        Assert.DoesNotContain("exit 0", e2eStep);
    }

    /// <summary>
    /// Verifies the root E2E wrapper remains the signed-package cleanup-aware proof gate.
    /// </summary>
    [TestMethod]
    public void E2eWrapperBuildsSignedPackageAndValidatesCleanup()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2eWrapperPath = Path.Combine(repositoryRoot, "test-e2e.ps1");
        string e2eWrapper = File.ReadAllText(e2eWrapperPath);

        Assert.Contains("Assert-Administrator", e2eWrapper);
        Assert.Contains("New-SelfSignedCertificate", e2eWrapper);
        Assert.Contains("Get-PrintSinkPackageCertificate", e2eWrapper);
        Assert.Contains("Build-PrintSinkPackage", e2eWrapper);
        Assert.Contains("/p:GenerateAppxPackageOnBuild=true", e2eWrapper);
        Assert.Contains("/p:AppxPackageSigningEnabled=true", e2eWrapper);
        Assert.Contains("/p:PackageCertificateThumbprint=$($Certificate.Thumbprint)", e2eWrapper);
        Assert.Contains("Add-CertificateToStore", e2eWrapper);
        Assert.Contains("StoreName]::TrustedPeople", e2eWrapper);
        Assert.DoesNotContain("StoreName]::Root", e2eWrapper);
        Assert.Contains("$e2eParameters.Cleanup = $true", e2eWrapper);
        Assert.Contains("Assert-PrintSinkE2EResult.ps1", e2eWrapper);
        Assert.Contains("$resultAssertionParameters.RequireCleanup = $true", e2eWrapper);
        AssertBefore(e2eWrapper, "& $e2eScript @e2eParameters", "& $resultAssertionScript @resultAssertionParameters");
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

    /// <summary>
    /// Verifies supported E2E feature evidence must be marked as passed in the persisted result.
    /// </summary>
    [TestMethod]
    public void E2eResultValidatorRequiresPassedFeatureEvidence()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string e2eScript = File.ReadAllText(e2ePath);
        string validatorScript = File.ReadAllText(validatorPath);

        Assert.Contains("passed = $true", e2eScript);
        Assert.Contains("Get-ResultProperty -Object $evidence -Name 'passed'", validatorScript);
        Assert.Contains("Feature evidence #$number was not marked as passed.", validatorScript);
    }

    /// <summary>
    /// Verifies the live E2E result validator parses real output documents instead of checking only file presence.
    /// </summary>
    [TestMethod]
    public void E2eResultValidatorRequiresRealDocumentAssertions()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string documentAssertionsPath = Path.Combine(
            repositoryRoot,
            "tests",
            "PrintSink.E2E.Assertions",
            "DocumentAssertions.cs");
        string packagesPath = Path.Combine(repositoryRoot, "Directory.Packages.props");
        string validatorScript = File.ReadAllText(validatorPath);
        string documentAssertions = File.ReadAllText(documentAssertionsPath);
        string packages = File.ReadAllText(packagesPath);

        Assert.Contains("PdfPig", packages);
        Assert.Contains("PdfDocument.Open(path)", documentAssertions);
        Assert.Contains("ContentOrderTextExtractor.GetText(page)", documentAssertions);
        Assert.Contains("private static void AssertXps", documentAssertions);
        Assert.Contains("private static void AssertPostScript", documentAssertions);
        Assert.Contains("private static void AssertPwgRaster", documentAssertions);
        Assert.Contains("private static void AssertPclm", documentAssertions);

        Assert.Contains("Assert-Document -Format 'pdf' -Path $pdf.outputPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'oxps' -Path $xps.outputPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'postscript' -Path $postScript.outputPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'pwg' -Path $pwg.outputPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'pclm' -Path $pclm.outputPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'pdf' -Path $cloudArtifact.artifactCopyPath", validatorScript);
        Assert.Contains("Assert-Document -Format 'pdf' -Path $notepad.outputPath", validatorScript);
        Assert.Contains("Assert-FilesEqual -ExpectedPath $pdfPassthrough.sourcePath", validatorScript);
        Assert.Contains("Assert-EmptyOrMissingFile -Path $failedImageWatermark.outputPath", validatorScript);
        Assert.Contains("Assert-EmptyOrMissingFile -Path $jobUiCancel.outputPath", validatorScript);
    }

    /// <summary>
    /// Verifies the live E2E suite must prove installed queue persistence after every major workflow.
    /// </summary>
    [TestMethod]
    public void E2eRequiresQueuePersistenceEvidence()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string e2eScript = File.ReadAllText(e2ePath);

        Assert.Contains("function Assert-PrintSinkQueuePersistence", e2eScript);
        Assert.Contains("function Assert-PrintSinkQueuesRemoved", e2eScript);
        Assert.Contains("$queuePersistenceResult = Assert-PrintSinkQueuePersistence", e2eScript);
        Assert.Contains("-QueuePersistence $queuePersistenceResult", e2eScript);
        Assert.Contains("queuePersistence = $queuePersistenceResult", e2eScript);
        Assert.Contains("$result['cleanup'] = $cleanupResult", e2eScript);
        Assert.Contains("Assert-PrintSinkQueuesRemoved", e2eScript);
        Assert.Contains("Queue persistence failed", e2eScript);
        AssertBefore(
            e2eScript,
            "$queuePersistenceResult = Assert-PrintSinkQueuePersistence",
            "$featureEvidence = New-PrintSinkFeatureEvidence");
        AssertBefore(
            e2eScript,
            "$result['cleanup'] = $cleanupResult",
            "Write-PrintSinkE2EResult -Result $result -ResultPath $resultPath | Out-Null");
    }

    /// <summary>
    /// Verifies the physical IPP E2E path proves non-default printer state traffic.
    /// </summary>
    [TestMethod]
    public void E2eIppAssociationExercisesStoppedRejectingPrinterState()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string e2eScript = File.ReadAllText(e2ePath);

        Assert.Contains("function Invoke-PrintSinkIppPrinterStateProbe", e2eScript);
        Assert.Contains("-PrinterState Stopped", e2eScript);
        Assert.Contains("-PrinterStateReason paused", e2eScript);
        Assert.Contains("-RejectJobs", e2eScript);
        Assert.Contains("printer-state", e2eScript);
        Assert.Contains("printer-state-reasons", e2eScript);
        Assert.Contains("printer-is-accepting-jobs", e2eScript);
        Assert.Contains("printerStateProbe", e2eScript);
    }

    /// <summary>
    /// Verifies the local E2E installer trusts the package-adjacent test certificate.
    /// </summary>
    [TestMethod]
    public void E2ePackageInstallTrustsAdjacentCertificate()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string e2eScript = File.ReadAllText(e2ePath);
        string packageTrustScript = ExtractScriptBlock(
            e2eScript,
            "function Import-PrintSinkPackageCertificate",
            "function Add-MediumIntegrityProcessLauncher");

        Assert.Contains("function Import-PrintSinkPackageCertificate", e2eScript);
        Assert.Contains("Add-PrintSinkPackageCertificateToStore", packageTrustScript);
        Assert.Contains("X509Store", packageTrustScript);
        Assert.Contains("StoreName]::TrustedPeople", packageTrustScript);
        Assert.Contains("StoreLocation]::CurrentUser", packageTrustScript);
        Assert.Contains("StoreLocation]::LocalMachine", packageTrustScript);
        Assert.IsFalse(
            packageTrustScript.Contains("StoreName]::Root", StringComparison.Ordinal),
            "E2E package trust must not write to a Root store.");
        Assert.Contains("Import-PrintSinkPackageCertificate -PackagePath $PackagePath", e2eScript);
        AssertBefore(e2eScript, "Import-PrintSinkPackageCertificate -PackagePath $PackagePath", "Add-AppxPackage");
    }

    private static string ExtractScriptBlock(string text, string startMarker, string endMarker)
    {
        int startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = text.IndexOf(endMarker, startIndex >= 0 ? startIndex : 0, StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, startIndex, $"Could not find '{startMarker}'.");
        Assert.IsGreaterThan(startIndex, endIndex, $"Could not find '{endMarker}' after '{startMarker}'.");

        return text[startIndex..endIndex];
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
