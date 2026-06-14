using System.Text.RegularExpressions;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the repository CI contract for real print-stack validation.
/// </summary>
[TestClass]
internal sealed partial class ContinuousIntegrationContractTests
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
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string design = File.ReadAllText(designPath);

        Assert.Contains("platform: x64", workflow);
        Assert.Contains("platform: ARM64", workflow);
        Assert.Contains("Real print-stack E2E", workflow);
        Assert.Contains(".\\build.ps1 -Configuration Release -Platform ${{ matrix.platform }}", workflow);
        Assert.Contains("MSTest on Microsoft.Testing.Platform for plain .NET projects", design);
        Assert.Contains("Visual Studio Test Platform for packaged WinUI app tests", design);
        Assert.Contains(".\\test-e2e.ps1 -BuildPackage -Configuration Release", design);
        Assert.Contains("signed Release MSIX", design);
        Assert.DoesNotContain("MSTest on Microsoft.Testing.Platform, .NET 10, plus scripted Windows E2E", design);
        Assert.Contains(".\\test-e2e.ps1", workflow);
        Assert.Contains("-BuildPackage", workflow);
        Assert.Contains("-Configuration Release", workflow);
        Assert.Contains("-Platform ${{ matrix.platform }}", workflow);
        Assert.DoesNotContain("tests\\e2e\\Invoke-PrintSinkE2E.ps1", workflow);
        Assert.DoesNotContain("New-SelfSignedCertificate", workflow);
        Assert.DoesNotContain("PackageCertificateThumbprint", workflow);
        Assert.IsFalse(
            workflow.Contains("StoreName]::Root", StringComparison.Ordinal),
            "CI package trust must not write to a Root store.");

        AssertBefore(workflow, "- name: Build", "- name: Release build");
        AssertBefore(workflow, "- name: Release build", "- name: Test");
        AssertBefore(workflow, "- name: Test", "- name: Packaged app tests");
        AssertBefore(workflow, "- name: Packaged app tests", "- name: Core coverage");
        AssertBefore(workflow, "- name: Core coverage", "- name: Real print-stack E2E");
        AssertBefore(workflow, "- name: Real print-stack E2E", "- name: Assert clean PrintSink state");
        AssertBefore(workflow, "- name: Assert clean PrintSink state", "- name: Upload test results");
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
            "- name: Assert clean PrintSink state");

        Assert.DoesNotContain("continue-on-error", e2eStep);
        Assert.DoesNotContain("if: always()", e2eStep);
        Assert.DoesNotContain("|| true", e2eStep);
        Assert.DoesNotContain("exit 0", e2eStep);
    }

    /// <summary>
    /// Verifies CI publishes the real E2E evidence and package artifacts as required outputs.
    /// </summary>
    [TestMethod]
    public void WindowsCiRequiresRealPrintStackEvidenceArtifacts()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "windows-ci.yml");
        string workflow = File.ReadAllText(workflowPath);
        string e2eStep = ExtractScriptBlock(
            workflow,
            "- name: Real print-stack E2E",
            "- name: Assert clean PrintSink state");
        string cleanStateStep = ExtractScriptBlock(
            workflow,
            "- name: Assert clean PrintSink state",
            "- name: Upload test results");
        string testResultsUploadStep = ExtractScriptBlock(
            workflow,
            "- name: Upload test results",
            "- name: Upload coverage");
        string coverageUploadStep = ExtractScriptBlock(
            workflow,
            "- name: Upload coverage",
            "- name: Upload E2E outputs");
        string e2eUploadStep = ExtractScriptBlock(
            workflow,
            "- name: Upload E2E outputs",
            "- name: Upload MSIX package");
        int msixUploadIndex = workflow.IndexOf("- name: Upload MSIX package", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, msixUploadIndex, "Could not find the MSIX upload step.");
        string msixUploadStep = workflow[msixUploadIndex..];

        Assert.Contains(".\\test-e2e.ps1 -BuildPackage -Configuration Release -Platform ${{ matrix.platform }}", e2eStep);
        Assert.DoesNotContain("-SkipPackageInstall", e2eStep);
        Assert.DoesNotContain("-KeepQueues", e2eStep);

        Assert.Contains("if: always()", cleanStateStep);
        Assert.Contains(".\\test-clean-state.ps1 -Cleanup", cleanStateStep);

        Assert.Contains("if: always()", testResultsUploadStep);
        Assert.Contains("uses: actions/upload-artifact@v7", testResultsUploadStep);
        Assert.Contains("name: test-results-${{ matrix.platform }}", testResultsUploadStep);
        Assert.Contains("path: artifacts/test-results/${{ matrix.platform }}/*.trx", testResultsUploadStep);
        Assert.Contains("if-no-files-found: error", testResultsUploadStep);
        Assert.DoesNotContain("if-no-files-found: ignore", testResultsUploadStep);

        Assert.Contains("if: always()", coverageUploadStep);
        Assert.Contains("uses: actions/upload-artifact@v7", coverageUploadStep);
        Assert.Contains("name: coverage-${{ matrix.platform }}", coverageUploadStep);
        Assert.Contains("path: artifacts/coverage/core.${{ matrix.platform }}.cobertura.xml", coverageUploadStep);
        Assert.Contains("if-no-files-found: error", coverageUploadStep);
        Assert.DoesNotContain("if-no-files-found: ignore", coverageUploadStep);

        Assert.Contains("if: always()", e2eUploadStep);
        Assert.Contains("uses: actions/upload-artifact@v7", e2eUploadStep);
        Assert.Contains("name: e2e-outputs-${{ matrix.platform }}", e2eUploadStep);
        Assert.Contains("path: artifacts/e2e/${{ matrix.platform }}", e2eUploadStep);
        Assert.Contains("if-no-files-found: error", e2eUploadStep);
        Assert.DoesNotContain("if-no-files-found: ignore", e2eUploadStep);

        Assert.Contains("if: always()", msixUploadStep);
        Assert.Contains("uses: actions/upload-artifact@v7", msixUploadStep);
        Assert.Contains("name: msix-${{ matrix.platform }}", msixUploadStep);
        Assert.Contains("path: artifacts/appxpackages/${{ matrix.platform }}", msixUploadStep);
        Assert.Contains("if-no-files-found: error", msixUploadStep);
        Assert.DoesNotContain("if-no-files-found: ignore", msixUploadStep);
    }

    /// <summary>
    /// Verifies the local build and testing docs describe the same Release MSIX E2E path that CI runs.
    /// </summary>
    [TestMethod]
    public void BuildAndTestingDocsUseReleaseMsixE2ePath()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string buildPath = Path.Combine(repositoryRoot, "docs", "BUILD.md");
        string testingPath = Path.Combine(repositoryRoot, "docs", "TESTING.md");
        string build = File.ReadAllText(buildPath);
        string testing = File.ReadAllText(testingPath);

        Assert.Contains(".\\test-e2e.ps1 -BuildPackage -Configuration Release -Platform x64", build);
        Assert.Contains(".\\test-e2e.ps1 -BuildPackage -Configuration Release -Platform x64", testing);
        Assert.Contains("signed Release MSIX", testing);
        Assert.DoesNotContain("_Debug_Test", build);
        Assert.DoesNotContain("_Debug_Test", testing);
        Assert.DoesNotContain("_x64_Debug", build);
        Assert.DoesNotContain("_x64_Debug", testing);
    }

    /// <summary>
    /// Verifies the root clean-state script detects and can clean leaked PrintSink state.
    /// </summary>
    [TestMethod]
    public void CleanStateScriptChecksPackagesQueuesAndProcesses()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string cleanStatePath = Path.Combine(repositoryRoot, "test-clean-state.ps1");
        string cleanStateScript = File.ReadAllText(cleanStatePath);

        Assert.Contains("[switch] $Cleanup", cleanStateScript);
        Assert.Contains("Get-AppxPackage 'PrintSink*'", cleanStateScript);
        Assert.Contains("Get-Printer -Name 'PrintSink*'", cleanStateScript);
        Assert.Contains("Get-CimInstance Win32_Process", cleanStateScript);
        Assert.Contains("Stop-Process -Id $processId", cleanStateScript);
        Assert.Contains("Remove-Printer -Name $queue", cleanStateScript);
        Assert.Contains("Remove-AppxPackage -Package $package", cleanStateScript);
        Assert.Contains("PrintSink package, queue, or process state was left behind.", cleanStateScript);
    }

    /// <summary>
    /// Verifies release MSIX packages use revision auto-incrementing without changing deterministic Debug E2E packages.
    /// </summary>
    [TestMethod]
    public void AppProjectAutoIncrementsPackageRevisionForReleaseOnly()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string appProjectPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "PrintSink.App.csproj");
        string appProject = File.ReadAllText(appProjectPath);

        Assert.Contains("<WindowsPackageType>MSIX</WindowsPackageType>", appProject);
        Assert.Contains("<EnableMsixTooling>true</EnableMsixTooling>", appProject);
        Assert.Contains("<RuntimeIdentifier Condition=\"'$(Configuration)' == 'Release' And '$(RuntimeIdentifier)' == '' And '$(Platform)' == 'x64'\">win-x64</RuntimeIdentifier>", appProject);
        Assert.Contains("<RuntimeIdentifier Condition=\"'$(Configuration)' == 'Release' And '$(RuntimeIdentifier)' == '' And '$(Platform)' == 'ARM64'\">win-arm64</RuntimeIdentifier>", appProject);
        Assert.Contains("<PublishReadyToRun Condition=\"'$(Configuration)' != 'Debug'\">True</PublishReadyToRun>", appProject);
        Assert.Contains("<PublishTrimmed>False</PublishTrimmed>", appProject);
        Assert.Contains("<AppxAutoIncrementPackageRevision Condition=\"'$(Configuration)' == 'Release'\">True</AppxAutoIncrementPackageRevision>", appProject);
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
        Assert.Contains("[switch] $KeepPackage", e2eWrapper);
        Assert.Contains("function Remove-PrintSinkPackage", e2eWrapper);
        Assert.Contains("Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json", e2eWrapper);
        Assert.Contains("Where-Object { $_.PackageFullName -eq $packageFullName }", e2eWrapper);
        Assert.Contains("Get-AppxPackage -Name 'PrintSink'", e2eWrapper);
        Assert.Contains("Remove-AppxPackage -Package $package.PackageFullName", e2eWrapper);
        Assert.Contains("$shouldRemovePackageAfterRun = (-not $SkipPackageInstall) -and (-not $KeepPackage) -and (-not $KeepQueues)", e2eWrapper);
        Assert.Contains("Assert-PrintSinkE2EResult.ps1", e2eWrapper);
        Assert.Contains("$resultAssertionParameters.RequireCleanup = $true", e2eWrapper);
        Assert.Contains("Remove-PrintSinkPackage -ResultPath $resultPath", e2eWrapper);
        AssertBefore(e2eWrapper, "& $e2eScript @e2eParameters", "& $resultAssertionScript @resultAssertionParameters");
        AssertBefore(e2eWrapper, "& $resultAssertionScript @resultAssertionParameters", "if ($shouldRemovePackageAfterRun) {");
    }

    /// <summary>
    /// Verifies every packaged app command, except help, is exercised by the real E2E suite.
    /// </summary>
    [TestMethod]
    public void PackagedAppCommandsAreCoveredByRealPrintStackE2e()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string commandLinePath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "VirtualPrinterCommandLine.cs");
        string routerPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "AppActivationRouter.cs");
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string commandLine = File.ReadAllText(commandLinePath);
        string router = File.ReadAllText(routerPath);
        string e2eScript = File.ReadAllText(e2ePath);

        string[] actualCommandSwitches =
        [
            .. AppCommandSwitchRegex()
                .Matches(commandLine)
                .Select(static match => match.Groups["switch"].Value),
            .. RouterCommandSwitchRegex()
                .Matches(router)
                .Select(static match => match.Groups["switch"].Value),
        ];
        actualCommandSwitches =
        [
            .. actualCommandSwitches
                .Where(static commandSwitch => commandSwitch != "--help")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static commandSwitch => commandSwitch, StringComparer.Ordinal),
        ];

        string[] expectedCommandSwitches =
        [
            "--assert-virtual-attribute-read",
            "--clear-watermark",
            "--disable-job-ui",
            "--enable-job-ui",
            "--install-virtual-printers",
            "--print-pdf-passthrough",
            "--refresh-capabilities",
            "--remove-virtual-printers",
            "--set-default-copies",
            "--set-image-watermark",
            "--set-text-watermark",
            "--winrt-source-print",
        ];

        CollectionAssert.AreEqual(
            expectedCommandSwitches,
            actualCommandSwitches,
            "Every packaged app command switch must be explicitly classified for real E2E coverage.");

        foreach (string commandSwitch in expectedCommandSwitches)
        {
            Assert.Contains(commandSwitch, e2eScript);
        }
    }

    /// <summary>
    /// Verifies the real print-stack E2E suite refuses unsupported Windows builds.
    /// </summary>
    [TestMethod]
    public void E2eRequiresSupportedWindowsBuild()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string e2eScript = File.ReadAllText(e2ePath);
        string validatorScript = File.ReadAllText(validatorPath);

        Assert.Contains("[Version]'10.0.26100.0'", e2eScript);
        Assert.Contains("OSVersion.Version", e2eScript);
        Assert.Contains("Current build:", e2eScript);
        Assert.Contains("[Version]'10.0.26100.0'", validatorScript);
        Assert.Contains("function Assert-SupportedWindowsVersion", validatorScript);
        Assert.Contains("Assert-SupportedWindowsVersion -WindowsVersion ([string]$result.windowsVersion)", validatorScript);
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
        Assert.Contains("Get-ResultProperty -Object $evidence -Name 'evidence'", validatorScript);
        Assert.Contains("Feature evidence #$number was not marked as passed.", validatorScript);
        Assert.Contains("Feature evidence #$number had no evidence description.", validatorScript);
        Assert.Contains("Feature evidence #$number had an empty artifact.", validatorScript);
        Assert.Contains("Deferred feature evidence #$number had no evidence description.", validatorScript);
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
    /// Verifies the live E2E result validator checks exact PDL routes for real output paths.
    /// </summary>
    [TestMethod]
    public void E2eResultValidatorRequiresExactRouteAssertions()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string validatorScript = File.ReadAllText(validatorPath);

        Assert.Contains("function Assert-Route", validatorScript);
        Assert.Contains("route was '$route'; expected '$ExpectedRoute'.", validatorScript);
        Assert.Contains("application/oxps -> Pdf; Convert; Convert XPS to PDF.", validatorScript);
        Assert.Contains("application/oxps -> Oxps; Copy; Endpoint supports passthrough.", validatorScript);
        Assert.Contains("application/postscript -> PostScript; Copy; Endpoint supports passthrough.", validatorScript);
        Assert.Contains("application/oxps -> PwgRaster; Convert; Convert XPS to PWG Raster.", validatorScript);
        Assert.Contains("application/oxps -> Pclm; Convert; Convert XPS to PCLm.", validatorScript);
        Assert.Contains("application/pdf -> Pdf; Copy; Endpoint supports passthrough.", validatorScript);
        Assert.Contains("Assert-Route -Result $pdf", validatorScript);
        Assert.Contains("Assert-Route -Result $xps", validatorScript);
        Assert.Contains("Assert-Route -Result $postScript", validatorScript);
        Assert.Contains("Assert-Route -Result $pwg", validatorScript);
        Assert.Contains("Assert-Route -Result $pclm", validatorScript);
        Assert.Contains("Assert-Route -Result $cloud", validatorScript);
        Assert.Contains("Assert-Route -Result $notepad", validatorScript);
        Assert.Contains("Assert-Route -Result $failedImageWatermark", validatorScript);
        Assert.Contains("Assert-Route -Result $jobUiWatermark", validatorScript);
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
        Assert.Contains("after management UI check", e2eScript);
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
    /// Verifies the real E2E suite proves the management UI exposes queue lifecycle actions.
    /// </summary>
    [TestMethod]
    public void E2eRequiresManagementUiQueueLifecycleActions()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string e2eScript = File.ReadAllText(e2ePath);
        string validatorScript = File.ReadAllText(validatorPath);

        Assert.Contains("function Invoke-PrintSinkManagementUi", e2eScript);
        Assert.Contains("'Install queues'", e2eScript);
        Assert.Contains("'Remove queues'", e2eScript);
        Assert.Contains("'Refresh queues'", e2eScript);
        Assert.Contains("'Refresh capabilities'", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Remove queues'", e2eScript);
        Assert.Contains("Wait-ForPrintSinkQueuesRemoved", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Install queues'", e2eScript);
        Assert.Contains("Wait-ForPrintSinkQueuesInstalled", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Refresh queues'", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Refresh capabilities'", e2eScript);
        Assert.Contains("function Set-SpinnerRangeValue", e2eScript);
        Assert.Contains("Set-SpinnerRangeValue -Root $window -Name 'Default copies' -Value 2", e2eScript);
        Assert.Contains("Set-SpinnerRangeValue -Root $window -Name 'Default copies' -Value 1", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Enable Job UI'", e2eScript);
        Assert.Contains("Invoke-Button -Root $window -Name 'Headless jobs'", e2eScript);
        Assert.Contains("Management UI queues refreshed", e2eScript);
        Assert.Contains("Management UI capabilities refreshed", e2eScript);
        Assert.Contains("Management UI default copies updated", e2eScript);
        Assert.Contains("Management UI Job UI mode updated", e2eScript);
        Assert.Contains("invokedActions = @('Remove queues', 'Install queues', 'Refresh queues', 'Refresh capabilities', 'Set default copies', 'Enable Job UI', 'Headless jobs')", e2eScript);
        Assert.Contains("removedQueues = $removedQueues", e2eScript);
        Assert.Contains("installedQueues = $installedQueues", e2eScript);
        Assert.Contains("queuesRefreshed = $queuesRefreshed", e2eScript);
        Assert.Contains("managementCapabilityRefresh = $managementCapabilityRefresh", e2eScript);
        Assert.Contains("extensionCapabilityRefresh = $extensionCapabilityRefresh", e2eScript);
        Assert.Contains("defaultCopiesSet = $defaultCopiesSet", e2eScript);
        Assert.Contains("defaultCopiesRestore = $defaultCopiesRestore", e2eScript);
        Assert.Contains("jobUiEnabled = $jobUiEnabled", e2eScript);
        Assert.Contains("jobUiHeadless = $jobUiHeadless", e2eScript);
        Assert.Contains("managementUi = $managementUiResult", e2eScript);
        Assert.Contains("-ManagementUi $managementUiResult", e2eScript);
        Assert.Contains("function Assert-ManagementUi", validatorScript);
        Assert.Contains("Management UI visible actions", validatorScript);
        Assert.Contains("Management UI invoked actions", validatorScript);
        Assert.Contains("Management UI removed queue names", validatorScript);
        Assert.Contains("Management UI installed queue names", validatorScript);
        Assert.Contains("Management UI did not record a queue-refresh diagnostic", validatorScript);
        Assert.Contains("Management UI did not record a capability-refresh diagnostic", validatorScript);
        Assert.Contains("Management UI default-copy set diagnostic did not verify two copies", validatorScript);
        Assert.Contains("Management UI default-copy restore diagnostic did not verify one copy", validatorScript);
        Assert.Contains("Management UI did not record the Job UI enabled diagnostic", validatorScript);
        Assert.Contains("Management UI did not record the headless jobs diagnostic", validatorScript);
        Assert.Contains("Assert-ManagementUi -ManagementUi $result.managementUi", validatorScript);
        Assert.Contains("after management UI check", validatorScript);
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

    [GeneratedRegex(@"Contains\(commandArgs,\s*""(?<switch>--[a-z0-9-]+)""\)", RegexOptions.CultureInvariant)]
    private static partial Regex AppCommandSwitchRegex();

    [GeneratedRegex(@"WinRtPrintSourceSwitch\s*=\s*""(?<switch>--[a-z0-9-]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex RouterCommandSwitchRegex();
}
