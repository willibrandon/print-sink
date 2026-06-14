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
