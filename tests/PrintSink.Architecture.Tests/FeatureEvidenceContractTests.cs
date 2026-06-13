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
        string e2ePath = Path.Combine(repositoryRoot, "tests", "e2e", "Invoke-PrintSinkE2E.ps1");

        string design = File.ReadAllText(designPath);
        string e2eScript = File.ReadAllText(e2ePath);

        int[] designFeatureNumbers = ExtractDesignFeatureNumbers(design);
        int[] trackedDesignNumbers = ExtractTrackedDesignFeatureNumbers(design);
        int[] supportedEvidenceNumbers = ExtractSupportedEvidenceNumbers(e2eScript);
        int[] deferredEvidenceNumbers = ExtractDeferredEvidenceNumbers(e2eScript);
        int[] evidenceNumbers = [.. supportedEvidenceNumbers.Concat(deferredEvidenceNumbers).Order()];

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
        Assert.Contains("Rows 22 and 26 are tracked separately as deferred compatibility hooks", design);
        Assert.Contains("every supported feature in §4 implemented", design);
        Assert.DoesNotContain("no feature is descoped", design);
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

    private static int[] ExtractSupportedEvidenceNumbers(string e2eScript)
    {
        Match functionMatch = SupportedEvidenceFunctionRegex().Match(e2eScript);
        Assert.IsTrue(functionMatch.Success, "Could not find Assert-PrintSinkFeatureEvidenceComplete.");

        List<int> numbers = [];
        foreach (Match match in SupportedNumberAssignmentRegex().Matches(functionMatch.Groups["body"].Value))
        {
            string value = match.Groups["value"].Value;
            string? upperBound = match.Groups["upperBound"].Success
                ? match.Groups["upperBound"].Value
                : null;
            int lower = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (upperBound is null)
            {
                numbers.Add(lower);
                continue;
            }

            int upper = int.Parse(upperBound, System.Globalization.CultureInfo.InvariantCulture);
            numbers.AddRange(Enumerable.Range(lower, upper - lower + 1));
        }

        return [.. numbers.Distinct().Order()];
    }

    private static int[] ExtractDeferredEvidenceNumbers(string e2eScript)
    {
        Match functionMatch = DeferredEvidenceFunctionRegex().Match(e2eScript);
        Assert.IsTrue(functionMatch.Success, "Could not find New-PrintSinkDeferredFeatureEvidence.");

        return [.. DeferredNumberRegex()
            .Matches(functionMatch.Groups["body"].Value)
            .Select(static match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order()];
    }

    private static string ReadRepositoryFile(params string[] relativePathParts)
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string path = Path.Combine([repositoryRoot, .. relativePathParts]);
        return File.ReadAllText(path);
    }

    [GeneratedRegex(@"^\|\s*(?<number>\d+)\s*\|", RegexOptions.Multiline)]
    private static partial Regex DesignFeatureRowRegex();

    [GeneratedRegex(@"^\|\s*(?<number>\d+)\s*\|[^\r\n]*Tracked only\.", RegexOptions.Multiline)]
    private static partial Regex TrackedDesignFeatureRowRegex();

    [GeneratedRegex(@"function Assert-PrintSinkFeatureEvidenceComplete\s*\{(?<body>.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex SupportedEvidenceFunctionRegex();

    [GeneratedRegex(@"\$supportedNumbers\s*\+=\s*(?<value>\d+)(?:\.\.(?<upperBound>\d+))?")]
    private static partial Regex SupportedNumberAssignmentRegex();

    [GeneratedRegex(@"function New-PrintSinkDeferredFeatureEvidence\s*\{(?<body>.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex DeferredEvidenceFunctionRegex();

    [GeneratedRegex(@"number\s*=\s*(?<number>\d+)")]
    private static partial Regex DeferredNumberRegex();
}
