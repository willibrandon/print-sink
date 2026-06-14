using System.Text.RegularExpressions;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests the contract between the design feature matrix and E2E evidence reporting.
/// </summary>
[TestClass]
internal sealed partial class FeatureEvidenceContractTests
{
    /// <summary>
    /// Verifies every design feature row is either supported by E2E evidence or explicitly deferred.
    /// </summary>
    [TestMethod]
    public void DesignFeatureRowsMatchE2eSupportedAndDeferredEvidence()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string featureMatrixPath = Path.Combine(repositoryRoot, "tests", "e2e", "PrintSinkFeatureMatrix.ps1");
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorPath = Path.Combine(repositoryRoot, "tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        string design = File.ReadAllText(designPath);
        string featureMatrixScript = File.ReadAllText(featureMatrixPath);
        string e2eScript = File.ReadAllText(e2ePath);
        string validatorScript = File.ReadAllText(validatorPath);

        int[] designFeatureNumbers = ExtractDesignFeatureNumbers(design);
        int[] trackedDesignNumbers = ExtractTrackedDesignFeatureNumbers(design);
        Dictionary<int, string> designFeatureNames = ExtractDesignFeatureNames(design);
        Dictionary<int, string> supportedEvidenceNames = ExtractSupportedEvidenceNames(e2eScript);
        Dictionary<int, string> deferredEvidenceNames = ExtractDeferredEvidenceNames(e2eScript);
        int[] supportedEvidenceNumbers = [.. supportedEvidenceNames.Keys.Order()];
        int[] deferredEvidenceNumbers = [.. deferredEvidenceNames.Keys.Order()];
        int[] evidenceNumbers = [.. supportedEvidenceNumbers.Concat(deferredEvidenceNumbers).Order()];
        const string featureMatrixSource = ". (Join-Path $PSScriptRoot 'PrintSinkFeatureMatrix.ps1')";

        Assert.IsEmpty(
            supportedEvidenceNumbers.Intersect(deferredEvidenceNumbers).ToArray(),
            "Supported and deferred E2E feature evidence numbers must not overlap.");
        CollectionAssert.AreEqual(
            designFeatureNumbers,
            evidenceNumbers,
            "Every design feature row must be represented in supported or deferred E2E evidence.");
        CollectionAssert.AreEqual(
            trackedDesignNumbers,
            deferredEvidenceNumbers,
            "Tracked-only design rows must match deferred E2E evidence numbers.");

        CollectionAssert.AreEqual(
            designFeatureNumbers,
            designFeatureNames.Keys.Order().ToArray(),
            "Every design feature row must have a parsed feature name.");
        AssertEvidenceNamesMatchDesign(designFeatureNames, supportedEvidenceNames, "supported");
        AssertEvidenceNamesMatchDesign(designFeatureNames, deferredEvidenceNames, "deferred");
        Assert.Contains("Get-PrintSinkDesignFeatureMatrix", featureMatrixScript);
        Assert.Contains("Tracked only", featureMatrixScript);
        Assert.Contains(featureMatrixSource, e2eScript);
        Assert.Contains(featureMatrixSource, validatorScript);
        Assert.Contains("Get-PrintSinkSupportedFeatureMap", e2eScript);
        Assert.Contains("Get-PrintSinkSupportedFeatureMap", validatorScript);
        Assert.Contains("Get-PrintSinkDeferredFeatureMap", validatorScript);
    }

    /// <summary>
    /// Verifies the design document does not overclaim platform-trigger-only compatibility hooks.
    /// </summary>
    [TestMethod]
    public void DesignDocumentSeparatesSupportedAndDeferredFeatures()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string design = File.ReadAllText(designPath);

        Assert.Contains("Rows 1-21, 23-25, and 27 are supported", design);
        Assert.Contains("Rows 22, 26, and 28 are tracked separately as deferred compatibility hooks", design);
        Assert.Contains("every supported feature in §4 implemented", design);
        Assert.DoesNotContain("no feature is descoped", design);
    }

    /// <summary>
    /// Verifies preferred input format evidence records both manifest data and observed job routes.
    /// </summary>
    [TestMethod]
    public void PreferredInputFormatEvidenceRequiresManifestAndObservedRoutes()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-PreferredInputFormatEvidence", e2eScript);
        Assert.Contains("manifestPreferredFormats", e2eScript);
        Assert.Contains("observedRoutes", e2eScript);
        Assert.Contains("matching source content types", e2eScript);

        Assert.Contains("Assert-PreferredInputFormatEvidence", validatorScript);
        Assert.Contains("preferredInputFormat = 'application/postscript'", validatorScript);
        Assert.Contains("preferredInputFormat = 'application/oxps'", validatorScript);
        Assert.Contains("$route.StartsWith(\"$($expectedPrinter.preferredInputFormat) ->\"", validatorScript);

        Assert.Contains("Manifest + E2E", design);
        Assert.Contains("real-job source content type", design);
        Assert.Contains("preferred input format for every virtual-printer manifest entry", testing);
        Assert.Contains("observed source content type", testing);
    }

    /// <summary>
    /// Verifies passthrough evidence records manifest supported formats and observed copy routes.
    /// </summary>
    [TestMethod]
    public void PassthroughFormatEvidenceRequiresManifestFormatsAndCopyRoutes()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-PassthroughFormatEvidence", e2eScript);
        Assert.Contains("New-VirtualPrinterSupportedFormatSummary", e2eScript);
        Assert.Contains("manifestSupportedFormats", e2eScript);
        Assert.Contains("observedCopyRoutes", e2eScript);
        Assert.Contains("PDF passthrough is byte-for-byte identical", e2eScript);

        Assert.Contains("Assert-PassthroughFormatEvidence", validatorScript);
        Assert.Contains("expectedVirtualPrinterSupportedFormats", validatorScript);
        Assert.Contains("Assert-SupportedFormatEvidence", validatorScript);
        Assert.Contains("Assert-FilesEqual -ExpectedPath $pdf.sourcePath", validatorScript);
        Assert.Contains("application/pdf -> Pdf; Copy; Endpoint supports passthrough.", validatorScript);
        Assert.Contains("application/oxps -> Oxps; Copy; Endpoint supports passthrough.", validatorScript);
        Assert.Contains("application/postscript -> PostScript; Copy; Endpoint supports passthrough.", validatorScript);

        Assert.Contains("`SupportedFormat` declarations", design);
        Assert.Contains("byte-for-byte PDF passthrough", design);
        Assert.Contains("supported passthrough format declarations", testing);
        Assert.Contains("observed copy routes", testing);
    }

    /// <summary>
    /// Verifies Save-As evidence records exact file-backed queues and real output files.
    /// </summary>
    [TestMethod]
    public void FilePrinterSaveAsEvidenceRequiresRealOutputFiles()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-FileBackedOutputEvidence", e2eScript);
        Assert.Contains("Test-FileOutputResult", e2eScript);
        Assert.Contains("notepad-command-line-print", e2eScript);
        Assert.Contains("validated files for every file-backed queue", e2eScript);

        Assert.Contains("Assert-FilePrinterSaveAsEvidence", validatorScript);
        Assert.Contains("expectedFileBackedOutputs", validatorScript);
        Assert.Contains("File-printer Save As harness queue names", validatorScript);
        Assert.Contains("Notepad Save As feature evidence", validatorScript);
        Assert.Contains("notepad-command-line-print", validatorScript);

        Assert.Contains("VirtualPrinterBackgroundTask + E2E", design);
        Assert.Contains("real Save-As output files", design);
        Assert.Contains("exact file-backed queue names", testing);
        Assert.Contains("validated Notepad `/p` PDF", testing);
    }

    /// <summary>
    /// Verifies sink, conversion, and copy feature evidence validates exact real outputs.
    /// </summary>
    [TestMethod]
    public void SinkConversionAndCopyEvidenceRequiresExactRealOutputs()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-CloudSinkEvidence", e2eScript);
        Assert.Contains("Test-ConvertedOutputEvidence", e2eScript);
        Assert.Contains("Test-XpsCopyEvidence", e2eScript);
        Assert.Contains("package-local PDF sink artifact", e2eScript);
        Assert.Contains("exact PDF, PWG Raster, and PCLm queue outputs", e2eScript);
        Assert.Contains("exact OXPS copy route", e2eScript);

        Assert.Contains("Assert-CloudSinkEvidence", validatorScript);
        Assert.Contains("Assert-ConvertedOutputEvidence", validatorScript);
        Assert.Contains("Assert-XpsCopyEvidence", validatorScript);
        Assert.Contains("expectedConvertedOutputs", validatorScript);
        Assert.Contains("expectedXpsCopyOutput", validatorScript);
        Assert.Contains("Conversion feature output queue names", validatorScript);

        Assert.Contains("validated PDF text", design);
        Assert.Contains("exact PDF/PWG-Raster/PCLm converted queue set", design);
        Assert.Contains("validated OXPS output containing source text", design);
        Assert.Contains("Conversion evidence must include exact converted queue names", testing);
        Assert.Contains("Cloud evidence must prove no Save-As output", testing);
    }

    /// <summary>
    /// Verifies watermark feature evidence validates each real watermark output.
    /// </summary>
    [TestMethod]
    public void WatermarkEvidenceRequiresTextImageAndJobUiOutputs()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-WatermarkEvidence", e2eScript);
        Assert.Contains("Default text watermark, default image watermark, and per-job UI watermark", e2eScript);
        Assert.Contains("exact real PDF artifacts with matching routes and validated content", e2eScript);

        Assert.Contains("Assert-WatermarkEvidence", validatorScript);
        Assert.Contains("Assert-PdfWatermarkResult", validatorScript);
        Assert.Contains("Default text watermark feature evidence", validatorScript);
        Assert.Contains("Default image watermark feature evidence", validatorScript);
        Assert.Contains("Job UI text watermark feature evidence", validatorScript);
        Assert.Contains("CI DEFAULT WATERMARK", validatorScript);
        Assert.Contains("CI WATERMARK", validatorScript);
        Assert.Contains("-RequiresImage", validatorScript);
        Assert.Contains("Watermark Job UI PDL evidence", validatorScript);

        Assert.Contains("PrintSink.Xps + E2E", design);
        Assert.Contains("default text, default image, and per-job UI watermark outputs", design);
        Assert.Contains("image-content evidence", design);
        Assert.Contains("Watermark feature evidence must include the default text, default image, and Job UI text watermark", testing);
        Assert.Contains("image-content validation", testing);
    }

    /// <summary>
    /// Verifies Job UI preview evidence records the real window and UI Automation path.
    /// </summary>
    [TestMethod]
    public void JobUiPreviewEvidenceRequiresRealWindowAndInteraction()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-JobUiPreviewEvidence", e2eScript);
        Assert.Contains("jobUiWindowTitle", e2eScript);
        Assert.Contains("saveAsDialogObserved", e2eScript);
        Assert.Contains("watermarkToggleSet", e2eScript);
        Assert.Contains("jobPasswordFieldUsed", e2eScript);
        Assert.Contains("continueInvoked", e2eScript);
        Assert.Contains("renderErrorAbsent", e2eScript);
        Assert.Contains("without exposing the password", e2eScript);

        Assert.Contains("Assert-JobUiPreviewEvidence", validatorScript);
        Assert.Contains("Job UI preview evidence reported the wrong window title.", validatorScript);
        Assert.Contains("Job UI preview evidence did not prove the Save As dialog was observed.", validatorScript);
        Assert.Contains("Job UI preview evidence did not prove Continue was invoked.", validatorScript);
        Assert.Contains("Job UI preview evidence leaked the job-password secret in diagnostics.", validatorScript);

        Assert.Contains("Job UI + E2E", design);
        Assert.Contains("real packaged Job preview window", design);
        Assert.Contains("no Reactor render error", design);
        Assert.Contains("Save As dialog observation", testing);
        Assert.Contains("UI Automation edits", testing);
    }

    /// <summary>
    /// Verifies Settings UI evidence records the real modal owner lifecycle.
    /// </summary>
    [TestMethod]
    public void SettingsUiEvidenceRequiresModalOwnerLifecycle()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");
        string testing = ReadRepositoryFile("docs", "TESTING.md");

        Assert.Contains("Test-SettingsUiOwnerEvidence", e2eScript);
        Assert.Contains("ownerWindowTitle", e2eScript);
        Assert.Contains("settingsWindowTitle", e2eScript);
        Assert.Contains("ownerRestored", e2eScript);
        Assert.Contains("renderErrorAbsent", e2eScript);
        Assert.Contains("no Reactor render error was present", e2eScript);

        Assert.Contains("Assert-SettingsUiOwner", validatorScript);
        Assert.Contains("Settings UI owner evidence did not prove the owner was disabled while modal.", validatorScript);
        Assert.Contains("Settings UI owner evidence did not prove the owner was restored after close.", validatorScript);
        Assert.Contains("Settings UI owner evidence did not prove the Reactor surface rendered without error.", validatorScript);
        Assert.Contains("Settings UI owner printer selection", validatorScript);

        Assert.Contains("Settings UI + E2E", design);
        Assert.Contains("owner disabled/restored state", design);
        Assert.Contains("no Reactor render error", design);
        Assert.Contains("absence of Reactor render", testing);
        Assert.Contains("owner restored state when Settings closes", testing);
    }

    /// <summary>
    /// Verifies MXDC feature evidence records each configured output-quality value.
    /// </summary>
    [TestMethod]
    public void MxdcFeatureEvidenceRequiresPerOutputQualityMapping()
    {
        string extensionTask = ReadRepositoryFile("src", "PrintSink.Tasks", "PrintSupportExtensionBackgroundTask.cs");
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        const string expectedMxdcQualityDetail =
            "mxdcQuality=Text=Png,Draft=JpegHighCompression,Normal=JpegMediumCompression,High=JpegLowCompression,Photo=Png,Auto=JpegMediumCompression,Fax=JpegHighCompression";

        Assert.Contains("\"mxdcQuality=\"", extensionTask);
        Assert.Contains("Text={text}", extensionTask);
        Assert.Contains("Draft={draft}", extensionTask);
        Assert.Contains("Normal={normal}", extensionTask);
        Assert.Contains("High={high}", extensionTask);
        Assert.Contains("Photo={photographic}", extensionTask);
        Assert.Contains("Auto={automatic}", extensionTask);
        Assert.Contains("Fax={fax}", extensionTask);
        Assert.Contains(expectedMxdcQualityDetail, e2eScript);
        Assert.Contains(expectedMxdcQualityDetail, validatorScript);
    }

    /// <summary>
    /// Verifies PDC and PDR feature evidence records the applied custom names.
    /// </summary>
    [TestMethod]
    public void PdcAndPdrFeatureEvidenceRequiresAppliedCustomNames()
    {
        string extensionTask = ReadRepositoryFile("src", "PrintSink.Tasks", "PrintSupportExtensionBackgroundTask.cs");
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        const string expectedPdcFeatureDetail =
            "pdcFeatures=PageMediaSize,PageMediaType,JobInputBin,JobOutputBin,JobPageOrder,JobStapleAllDocuments,PageResolution,JobWatermarkMode";
        const string expectedPdcOptionDetail =
            "pdcOptions=Receipt80Millimeter,ArchivePaper,ThermalReceiptMedia,AutomationInputBin,AutomationOutputBin,OddPagesThenEvenPages,StapleUpperLeft,Dpi600,Dpi1200,WatermarkOff,WatermarkText,WatermarkImage";
        const string expectedPdrResourceDetail =
            "pdrResourceNames=ArchivePaper,AutomationInputBin,AutomationOutputBin,Dpi1200,Dpi600,JobWatermarkMode,OddPagesThenEvenPages,Receipt80Millimeter,StapleUpperLeft,ThermalReceiptMedia,WatermarkImage,WatermarkOff,WatermarkText";

        Assert.Contains("FormatAppliedPdcFeatures", extensionTask);
        Assert.Contains("FormatAppliedPdcOptions", extensionTask);
        Assert.Contains("FormatLocalizedResourceNames", extensionTask);
        Assert.Contains(expectedPdcFeatureDetail, e2eScript);
        Assert.Contains(expectedPdcFeatureDetail, validatorScript);
        Assert.Contains(expectedPdcOptionDetail, e2eScript);
        Assert.Contains(expectedPdcOptionDetail, validatorScript);
        Assert.Contains(expectedPdrResourceDetail, e2eScript);
        Assert.Contains(expectedPdrResourceDetail, validatorScript);
    }

    /// <summary>
    /// Verifies printer-selected feature evidence records the adaptive card and requested print fields.
    /// </summary>
    [TestMethod]
    public void PrinterSelectedFeatureEvidenceRequiresAdaptiveCardAndRequestedFields()
    {
        string extensionTask = ReadRepositoryFile("src", "PrintSink.Tasks", "PrintSupportExtensionBackgroundTask.cs");
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        string[] expectedDetails =
        [
            "adaptiveCard=set",
            "adaptiveCardVersion=1.0",
            "adaptiveCardPrinter=PrintSink - PDF",
            "additionalFields=requested",
            "requested=3",
            "features=PageMediaType,PageOutputQuality",
            "parameters=JobCopiesAllDocuments",
        ];

        Assert.Contains("JsonSerializer.Serialize(cardText)", extensionTask);
        Assert.Contains("SetAdaptiveCard", extensionTask);
        Assert.Contains("SetAdditionalFeatures(additionalFeatures)", extensionTask);
        Assert.Contains("SetAdditionalParameters(additionalParameters)", extensionTask);
        Assert.Contains("CreatePrintTicketElement(\"PageMediaType\")", extensionTask);
        Assert.Contains("CreatePrintTicketElement(\"PageOutputQuality\")", extensionTask);
        Assert.Contains("CreatePrintTicketElement(\"JobCopiesAllDocuments\")", extensionTask);

        foreach (string expectedDetail in expectedDetails)
        {
            Assert.Contains(expectedDetail, e2eScript);
            Assert.Contains(expectedDetail, validatorScript);
        }
    }

    /// <summary>
    /// Verifies capability-refresh evidence cannot reuse stale extension diagnostics.
    /// </summary>
    [TestMethod]
    public void CapabilityRefreshFeatureEvidenceRequiresRequestOrderedExtensionUpdate()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        Assert.Contains("Get-PrintSinkDiagnosticTimestamp", e2eScript);
        Assert.Contains("capabilityRefreshRequestedUtc", e2eScript);
        Assert.Contains("-StartedUtc $capabilityRefreshRequestedUtc", e2eScript);
        Assert.Contains("-StartedSkewSeconds 0", e2eScript);
        Assert.Contains("-Event $ManagementUi.extensionCapabilityRefresh", e2eScript);
        Assert.Contains("-Value ([string]$ManagementUi.capabilityRefreshRequestedUtc)", e2eScript);
        Assert.Contains("recorded a later Capabilities updated event", e2eScript);

        Assert.Contains("Assert-ResultTimestampIsNotBefore", validatorScript);
        Assert.Contains("-Later $extensionCapabilityRefresh", validatorScript);
        Assert.Contains("-Earlier $ManagementUi", validatorScript);
        Assert.Contains("-EarlierTimestampName 'capabilityRefreshRequestedUtc'", validatorScript);
        Assert.Contains("Management UI capability refresh extension diagnostic", validatorScript);
    }

    /// <summary>
    /// Verifies extension and default-ticket feature rows have explicit artifact validators.
    /// </summary>
    [TestMethod]
    public void ExtensionAndDefaultTicketEvidenceRequiresExplicitArtifactValidation()
    {
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");

        string[] expectedValidators =
        [
            "Assert-PrintTicketValidationEvidence",
            "Assert-PdcFeatureEvidence",
            "Assert-PdrFeatureEvidence",
            "Assert-CapabilityRefreshEvidence",
            "Assert-UserDefaultPrintTicketEvidence",
            "Assert-MxdcFeatureEvidence",
        ];

        foreach (string expectedValidator in expectedValidators)
        {
            Assert.Contains(expectedValidator, validatorScript);
        }

        Assert.Contains("Print-ticket validation feature queue names", validatorScript);
        Assert.Contains("status=Resolved", validatorScript);
        Assert.Contains("PDC feature evidence did not report the PDC option list.", validatorScript);
        Assert.Contains("PDR feature evidence did not report the localized resource names.", validatorScript);
        Assert.Contains("Capability-refresh feature extension diagnostic", validatorScript);
        Assert.Contains("verifiedCopies=$ExpectedCopies", validatorScript);
        Assert.Contains("MXDC feature evidence did not report the full output-quality mapping.", validatorScript);

        Assert.Contains("endpoint-specific `status=Resolved`", design);
        Assert.Contains("applied PDC feature and option sets", design);
        Assert.Contains("exact localized resource names", design);
        Assert.Contains("Management UI refresh paths", design);
        Assert.Contains("requested and verified counts", design);
        Assert.Contains("full Text/Draft/Normal/High/Photo/Auto/Fax image-quality mapping", design);
    }

    /// <summary>
    /// Verifies IPP, concurrency, cancel, password, and compression rows have explicit artifact validators.
    /// </summary>
    [TestMethod]
    public void RemainingRuntimeFeatureEvidenceRequiresExplicitArtifactValidation()
    {
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");
        string design = ReadRepositoryFile("docs", "DESIGN.md");

        string[] expectedValidators =
        [
            "Assert-IppAssociationEvidence",
            "Assert-VirtualPrinterAttributeReadEvidence",
            "Assert-ConcurrentPrintEvidence",
            "Assert-GracefulCancelAndFailEvidence",
            "Assert-JobPasswordEvidence",
            "Assert-IppWorkflowStartEvidence",
        ];

        foreach (string expectedValidator in expectedValidators)
        {
            Assert.Contains(expectedValidator, validatorScript);
        }

        Assert.Contains("IPP association operations", validatorScript);
        Assert.Contains("PSA_PrintSinkE2E_IPP_Pri21CF", validatorScript);
        Assert.Contains("Virtual-printer attribute-read evidence", validatorScript);
        Assert.Contains("Concurrent print queues", validatorScript);
        Assert.Contains("Failed-job evidence", validatorScript);
        Assert.Contains("Canceled-job PDL evidence", validatorScript);
        Assert.Contains("Job-password evidence leaked the password secret.", validatorScript);
        Assert.Contains("IPP workflow-start evidence reported an IPP compression probe error.", validatorScript);

        Assert.Contains("signed INF publication", design);
        Assert.Contains("document-format-default", design);
        Assert.Contains("exact PCLm and cloud queue outputs", design);
        Assert.Contains("corrupt-image transform failure", design);
        Assert.Contains("without exposing the secret", design);
        Assert.Contains("no compression probe error", design);
    }

    /// <summary>
    /// Verifies localized queue-name evidence records the expected resource keys and installed names.
    /// </summary>
    [TestMethod]
    public void LocalizedQueueNameEvidenceRequiresExpectedResourceKeys()
    {
        string e2eScript = ReadRepositoryFile("tests", "e2e", "Invoke-PrintSinkE2E.ps1");
        string validatorScript = ReadRepositoryFile("tests", "e2e", "Assert-PrintSinkE2EResult.ps1");

        string[] expectedResourceKeys =
        [
            "ms-resource:PdfPrintDisplayName",
            "ms-resource:XpsPrintDisplayName",
            "ms-resource:PostScriptPrintDisplayName",
            "ms-resource:CloudPrintDisplayName",
            "ms-resource:PwgRasterPrintDisplayName",
            "ms-resource:PclmPrintDisplayName",
        ];

        Assert.Contains("Test-LocalizedQueueDisplayNameEvidence", e2eScript);
        Assert.Contains("Assert-LocalizedQueueNameEvidence", validatorScript);
        foreach (string expectedResourceKey in expectedResourceKeys)
        {
            Assert.Contains(expectedResourceKey, e2eScript);
            Assert.Contains(expectedResourceKey, validatorScript);
        }
    }

    /// <summary>
    /// Verifies deferred compatibility hooks remain implemented even though CI cannot trigger them deterministically.
    /// </summary>
    [TestMethod]
    public void DeferredCompatibilityHooksHaveDefensiveHandlers()
    {
        string jobPreviewScreen = ReadRepositoryFile("src", "PrintSink.App", "JobPreviewScreen.cs");
        string workflowTask = ReadRepositoryFile("src", "PrintSink.Tasks", "PrintSupportWorkflowBackgroundTask.cs");
        string extensionTask = ReadRepositoryFile("src", "PrintSink.Tasks", "PrintSupportExtensionBackgroundTask.cs");
        string printSupportContract19 = ReadRepositoryFile(
            "src",
            "PrintSink.Tasks",
            "UniversalApiContract19PrintSupport.cs");
        string printerContract19 = ReadRepositoryFile("src", "PrintSink.App", "UniversalApiContract19PrinterApis.cs");

        Assert.Contains("session.JobNotification += OnJobNotification", jobPreviewScreen);
        Assert.Contains("AppendJobNotificationDiagnostic", jobPreviewScreen);
        Assert.Contains("Job notification received", jobPreviewScreen);
        Assert.Contains("args.PrinterJob.GetJobStatus()", jobPreviewScreen);

        Assert.Contains("ApiInformation.IsEventPresent(PrintWorkflowJobBackgroundSessionType, \"JobIssueDetected\")", workflowTask);
        Assert.Contains("session.JobIssueDetected += OnJobIssueDetected", workflowTask);
        Assert.Contains("Workflow job issue detected", workflowTask);
        Assert.Contains("skipSystemErrorToast={args.SkipSystemErrorToast}", workflowTask);
        Assert.Contains("uiLaunch={(args.UILauncher.IsUILaunchEnabled() ? \"enabled\" : \"disabled\")}", workflowTask);

        Assert.Contains("ApiInformation.IsEventPresent(PrintSupportExtensionSessionType, \"CommunicationErrorDetected\")", extensionTask);
        Assert.Contains("session.CommunicationErrorDetected += OnCommunicationErrorDetected", extensionTask);
        Assert.Contains("IppCommunicationErrorKind.Timeout", extensionTask);
        Assert.Contains("ConfigureIppCommunicationTimeouts(args.CommunicationConfiguration)", extensionTask);
        Assert.Contains("IPP communication error", extensionTask);

        Assert.Contains("SetPdlPassthroughWithJobAttributesSupported", printSupportContract19);
        Assert.Contains("pdlPassthroughWithJobAttributes=enabled", printSupportContract19);
        Assert.Contains("IsPassthroughWithJobAttributesSupported", printerContract19);
        Assert.Contains("provider2=runtime-unusable", printerContract19);
        Assert.Contains("provider2Submit=fallback-v1", printerContract19);
        string pdlPassthroughPrintCommand = ReadRepositoryFile("src", "PrintSink.App", "PdlPassthroughPrintCommand.cs");
        Assert.Contains("provider2Submit=used", pdlPassthroughPrintCommand);
        Assert.Contains("ippAttributeSource=print-ticket-converter", pdlPassthroughPrintCommand);
        Assert.Contains("ippAttributeSource=minimal-fallback", pdlPassthroughPrintCommand);
        Assert.Contains("IppAttributeValue.CreateMimeMedia", pdlPassthroughPrintCommand);
        Assert.Contains("IppAttributeValue.CreateNameWithoutLanguage", pdlPassthroughPrintCommand);
        Assert.Contains("provider2Fallback={fallbackReason}", pdlPassthroughPrintCommand);
        Assert.Contains("ipp-attribute-conversion-failed", pdlPassthroughPrintCommand);
    }

    private static int[] ExtractDesignFeatureNumbers(string design)
    {
        return [.. DesignFeatureRowRegex()
            .Matches(design)
            .Select(static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Order()];
    }

    private static int[] ExtractTrackedDesignFeatureNumbers(string design)
    {
        return [.. TrackedDesignFeatureRowRegex()
            .Matches(design)
            .Select(static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Order()];
    }

    private static Dictionary<int, string> ExtractDesignFeatureNames(string design)
    {
        return DesignFeatureNameRegex()
            .Matches(design)
            .ToDictionary(
                static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                static match => match.Groups["feature"].Value.Trim());
    }

    private static Dictionary<int, string> ExtractSupportedEvidenceNames(string e2eScript)
    {
        Match functionMatch = FeatureEvidenceFunctionRegex().Match(e2eScript);
        Assert.IsTrue(functionMatch.Success, "Could not find New-PrintSinkFeatureEvidence.");

        return SupportedEvidenceNameRegex()
            .Matches(functionMatch.Groups["body"].Value)
            .ToDictionary(
                static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                static match => match.Groups["feature"].Value);
    }

    private static Dictionary<int, string> ExtractDeferredEvidenceNames(string e2eScript)
    {
        Match functionMatch = DeferredEvidenceFunctionRegex().Match(e2eScript);
        Assert.IsTrue(functionMatch.Success, "Could not find New-PrintSinkDeferredFeatureEvidence.");

        return DeferredEvidenceNameRegex()
            .Matches(functionMatch.Groups["body"].Value)
            .ToDictionary(
                static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                static match => match.Groups["feature"].Value);
    }

    private static void AssertEvidenceNamesMatchDesign(
        Dictionary<int, string> designFeatureNames,
        Dictionary<int, string> evidenceFeatureNames,
        string evidenceKind)
    {
        foreach ((int number, string evidenceName) in evidenceFeatureNames)
        {
            Assert.IsTrue(
                designFeatureNames.TryGetValue(number, out string? designName),
                $"The {evidenceKind} evidence row #{number} does not exist in DESIGN.md.");
            Assert.AreEqual(
                designName,
                evidenceName,
                $"The {evidenceKind} evidence name for row #{number} must match DESIGN.md.");
        }
    }

    private static string ReadRepositoryFile(params string[] relativePathParts)
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string path = Path.Combine([repositoryRoot, .. relativePathParts]);
        return File.ReadAllText(path);
    }

    [GeneratedRegex(@"^\|\s*(?<number>\d+)\s*\|", RegexOptions.Multiline)]
    private static partial Regex DesignFeatureRowRegex();

    [GeneratedRegex(@"^\|\s*(?<number>\d+)\s*\|\s*(?<feature>[^|]+?)\s*\|", RegexOptions.Multiline)]
    private static partial Regex DesignFeatureNameRegex();

    [GeneratedRegex(@"^\|\s*(?<number>\d+)\s*\|[^\r\n]*Tracked only\.", RegexOptions.Multiline)]
    private static partial Regex TrackedDesignFeatureRowRegex();

    [GeneratedRegex(@"function New-PrintSinkDeferredFeatureEvidence\s*\{(?<body>.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex DeferredEvidenceFunctionRegex();

    [GeneratedRegex(@"function New-PrintSinkFeatureEvidence\s*\{(?<body>.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FeatureEvidenceFunctionRegex();

    [GeneratedRegex(@"Add-PrintSinkFeatureEvidence[\s\S]*?-Number\s+(?<number>\d+)\s*`[\s\S]*?-Feature\s+'(?<feature>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex SupportedEvidenceNameRegex();

    [GeneratedRegex(@"number\s*=\s*(?<number>\d+)[\s\S]*?feature\s*=\s*'(?<feature>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex DeferredEvidenceNameRegex();

}
