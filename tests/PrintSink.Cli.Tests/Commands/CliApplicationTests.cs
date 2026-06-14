using PrintSink.Cli;
using PrintSink.Cli.Commands;
using System.CommandLine;

namespace PrintSink.Cli.Tests.Commands;

/// <summary>
/// Tests the command-line surface.
/// </summary>
[TestClass]
internal sealed class CliApplicationTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that root help lists the documented command surface.
    /// </summary>
    [TestMethod]
    public async Task RootHelpListsDocumentedCommands()
    {
        (int exitCode, string output, string error) = await InvokeAsync("--help").ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.AreEqual(string.Empty, error);
        Assert.Contains("PrintSink developer and operator tooling.", output);
        Assert.Contains("queues    List the PrintSink virtual queues.", output);
        Assert.Contains("manifest  Inspect the MSIX package manifest.", output);
        Assert.Contains("pdc       Inspect print device capability XML.", output);
        Assert.Contains("ticket    Inspect print-ticket fixtures.", output);
        Assert.Contains("sink      Exercise sink routing without print activation.", output);
        Assert.Contains("tui       Start the Hex1b diagnostics dashboard.", output);
    }

    /// <summary>
    /// Verifies that the queue command prints the built-in endpoint list.
    /// </summary>
    [TestMethod]
    public async Task QueuesWritesBuiltinEndpointNames()
    {
        (int exitCode, string output, _) = await InvokeAsync("queues").ConfigureAwait(false);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Installed", output);
        Assert.Contains("PrintSink - PDF", output);
        Assert.Contains("PrintSink - PWG Raster", output);
        Assert.Contains("PrintSink - PCLm", output);
        Assert.DoesNotContain("\t", output);
    }

    /// <summary>
    /// Verifies that the queue command marks queues found in the local print stack.
    /// </summary>
    [TestMethod]
    public async Task QueuesReportsInstalledStatusWhenSnapshotIsAvailable()
    {
        (int exitCode, string output, string error) = await InvokeQueuesAsync(
            PrinterQueueSnapshot.Available(["printsink - pdf"])).ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.AreEqual(string.Empty, error);
        Assert.Contains("PrintSink - PDF         Pdf         Oxps        .pdf        yes", output);
        Assert.Contains("PrintSink - XPS         Oxps        Oxps        .xps,.oxps  no", output);
    }

    /// <summary>
    /// Verifies that the queue command stays useful when installed queue inspection is unavailable.
    /// </summary>
    [TestMethod]
    public async Task QueuesReportsUnknownStatusWhenSnapshotIsUnavailable()
    {
        (int exitCode, string output, string error) = await InvokeQueuesAsync(
            PrinterQueueSnapshot.Unavailable("print stack unavailable")).ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.Contains("PrintSink - PDF         Pdf         Oxps        .pdf        unknown", output);
        Assert.Contains("PrintSink - XPS         Oxps        Oxps        .xps,.oxps  unknown", output);
        Assert.Contains("warning: installed queue status unavailable: print stack unavailable", error);
    }

    /// <summary>
    /// Verifies that the queue install command delegates provisioning to the packaged app.
    /// </summary>
    [TestMethod]
    public async Task QueuesInstallInvokesPackagedAppProvisioning()
    {
        string? packageArgument = null;
        (int exitCode, string output, string error) = await InvokeQueuesAsync(
            "install",
            PrinterQueueSnapshot.Available(["PrintSink - PDF"]),
            (argument, _) =>
            {
                packageArgument = argument;
                return Task.FromResult(CliExitCodes.Success);
            }).ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.AreEqual("--install-virtual-printers", packageArgument);
        Assert.AreEqual(string.Empty, error);
        Assert.Contains("install completed.", output);
        Assert.Contains("PrintSink - PDF         Pdf         Oxps        .pdf        yes", output);
    }

    /// <summary>
    /// Verifies that the queue remove command delegates cleanup to the packaged app.
    /// </summary>
    [TestMethod]
    public async Task QueuesRemoveInvokesPackagedAppCleanup()
    {
        string? packageArgument = null;
        (int exitCode, string output, string error) = await InvokeQueuesAsync(
            "remove",
            PrinterQueueSnapshot.Available([]),
            (argument, _) =>
            {
                packageArgument = argument;
                return Task.FromResult(CliExitCodes.Success);
            }).ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.AreEqual("--remove-virtual-printers", packageArgument);
        Assert.AreEqual(string.Empty, error);
        Assert.Contains("remove completed.", output);
        Assert.Contains("PrintSink - PDF         Pdf         Oxps        .pdf        no", output);
    }

    /// <summary>
    /// Verifies that package command failures are surfaced by queue provisioning commands.
    /// </summary>
    [TestMethod]
    public async Task QueuesInstallReportsPackagedAppFailure()
    {
        (int exitCode, string output, string error) = await InvokeQueuesAsync(
            "install",
            PrinterQueueSnapshot.Available([]),
            (_, _) => Task.FromResult(CliExitCodes.ValidationFailed)).ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.Contains("Package command failed with exit code 1.", error);
    }

    /// <summary>
    /// Verifies that sink testing uses the core PDL router.
    /// </summary>
    [TestMethod]
    public async Task SinkTestReportsConversionRoute()
    {
        (int exitCode, string output, _) = await InvokeAsync(
            "sink",
            "test",
            "--endpoint",
            "pdf",
            "--content-type",
            "application/oxps").ConfigureAwait(false);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Action: Convert", output);
        Assert.Contains("Conversion: XpsToPdf", output);
    }

    /// <summary>
    /// Verifies sink testing writes passthrough fixture bytes to a file-backed endpoint.
    /// </summary>
    [TestMethod]
    public async Task SinkTestWritesPassthroughFixtureToOutput()
    {
        string directory = CreateTestDirectory();
        string inputPath = Path.Combine(directory, "input.pdf");
        string outputPath = Path.Combine(directory, "output.pdf");
        byte[] inputBytes = "%PDF-1.7 fixture"u8.ToArray();
        await File.WriteAllBytesAsync(inputPath, inputBytes, TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync(
                "sink",
                "test",
                "--endpoint",
                "pdf",
                "--content-type",
                "application/pdf",
                "--input",
                inputPath,
                "--output",
                outputPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.Success, exitCode);
            Assert.Contains("Status: Succeeded", output);
            Assert.Contains($"Output: {outputPath}", output);
            CollectionAssert.AreEqual(inputBytes, await File.ReadAllBytesAsync(outputPath, TestContext.CancellationToken).ConfigureAwait(false));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies sink testing writes deterministic converted fixture bytes to a file-backed endpoint.
    /// </summary>
    [TestMethod]
    public async Task SinkTestWritesConvertedFixtureToOutput()
    {
        string directory = CreateTestDirectory();
        string inputPath = Path.Combine(directory, "input.oxps");
        string outputPath = Path.Combine(directory, "output.pdf");
        await File.WriteAllTextAsync(inputPath, "xps fixture", TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync(
                "sink",
                "test",
                "--endpoint",
                "pdf",
                "--content-type",
                "application/oxps",
                "--input",
                inputPath,
                "--output",
                outputPath).ConfigureAwait(false);

            string outputText = await File.ReadAllTextAsync(outputPath, TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.Success, exitCode);
            Assert.Contains("Conversion: XpsToPdf", output);
            Assert.IsTrue(outputText.StartsWith("PrintSink fixture conversion: XpsToPdf", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies cloud sink tests reject file-output paths.
    /// </summary>
    [TestMethod]
    public async Task SinkTestRejectsOutputPathForCloudEndpoint()
    {
        (int exitCode, _, string error) = await InvokeAsync(
            "sink",
            "test",
            "--endpoint",
            "cloud",
            "--content-type",
            "application/pdf",
            "--output",
            "cloud.pdf").ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
        Assert.Contains("does not accept --output", error);
    }

    /// <summary>
    /// Verifies manifest linting against a packaged virtual-printer manifest fixture.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintAcceptsVirtualPrinterManifest()
    {
        string directory = await CreateManifestFixtureAsync(ValidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(0, exitCode);
            Assert.Contains("ok: manifest package shape is valid.", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects supported formats without an explicit maximum version.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsSupportedFormatMissingMaxVersion()
    {
        string invalidManifest = ValidManifest.Replace(
            " Type=\"application/oxps\" MaxVersion=\"1.0\"",
            " Type=\"application/oxps\"",
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("SupportedFormat application/oxps must declare MaxVersion", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects an XPS endpoint that does not expose every expected file extension.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsXpsEndpointMissingOutputExtension()
    {
        string invalidManifest = ValidManifest.Replace(
            "OutputFileTypes=\"xps;oxps\"",
            "OutputFileTypes=\"oxps\"",
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("OutputFileTypes must include 'xps'", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects file output on the cloud endpoint.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsCloudOutputFileTypes()
    {
        string directory = await CreateManifestFixtureAsync(InvalidCloudOutputManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("must omit OutputFileTypes", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects foreground print-support UI extensions without entry points.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsForegroundExtensionWithoutEntryPoint()
    {
        string invalidManifest = ValidManifest.Replace(
            """<printsupport:Extension Category="windows.printSupportJobUI" EntryPoint="PrintSink.App.App" />""",
            """<printsupport:Extension Category="windows.printSupportJobUI" />""",
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("windows.printSupportJobUI extension must declare EntryPoint", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects manifests without the packaged app execution alias.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsMissingAppExecutionAlias()
    {
        string invalidManifest = ValidManifest.Replace(
            """
                    <uap3:Extension Category="windows.appExecutionAlias">
                      <uap3:AppExecutionAlias>
                        <desktop:ExecutionAlias Alias="printsink-app.exe" />
                      </uap3:AppExecutionAlias>
                    </uap3:Extension>
            """,
            string.Empty,
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("printsink-app.exe app execution alias", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects manifests that do not support concurrent print activations.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsMissingMultipleInstanceSupport()
    {
        string invalidManifest = ValidManifest.Replace(
            " uap10:SupportsMultipleInstances=\"true\"",
            string.Empty,
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("SupportsMultipleInstances", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects background print-support extensions with the wrong entry point.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsWrongBackgroundExtensionEntryPoint()
    {
        string invalidManifest = ValidManifest.Replace(
            """<printsupport:Extension Category="windows.printSupportWorkflow" EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask" />""",
            """<printsupport:Extension Category="windows.printSupportWorkflow" EntryPoint="PrintSink.Tasks.WrongTask" />""",
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("EntryPoint=\"PrintSink.Tasks.PrintSupportWorkflowBackgroundTask\"", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies manifest linting rejects missing WinRT activatable classes.
    /// </summary>
    [TestMethod]
    public async Task ManifestLintRejectsMissingActivatableClass()
    {
        string invalidManifest = ValidManifest.Replace(
            """<ActivatableClass ActivatableClassId="PrintSink.Xps.XpsSequentialDocument" ThreadingModel="both" />""",
            string.Empty,
            StringComparison.Ordinal);
        string directory = await CreateManifestFixtureAsync(invalidManifest).ConfigureAwait(false);

        try
        {
            string manifestPath = Path.Combine(directory, "Package.appxmanifest");
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("PrintSink.Xps.XpsSequentialDocument", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies PDC validation rejects files that are not Print Schema Framework v2 PDC documents.
    /// </summary>
    [TestMethod]
    public async Task PdcValidateReportsCoreShapeErrors()
    {
        string pdcPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.xml");
        await File.WriteAllTextAsync(pdcPath, "<PrintDeviceCapabilities />", TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync("pdc", "validate", "--pdc", pdcPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("error: PDC root element must use the Print Schema Framework v2 namespace.", output);
        }
        finally
        {
            File.Delete(pdcPath);
        }
    }

    /// <summary>
    /// Verifies PDC validation accepts matching PDR resources.
    /// </summary>
    [TestMethod]
    public async Task PdcValidateAcceptsMatchingPdrResources()
    {
        string directory = CreateTestDirectory();
        string pdcPath = Path.Combine(directory, "Printer.pdc.xml");
        string pdrPath = Path.Combine(directory, "Printer.pdr.xml");
        await File.WriteAllTextAsync(pdcPath, ValidCustomPdc, TestContext.CancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(pdrPath, ValidCustomPdr, TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync(
                "pdc",
                "validate",
                "--pdc",
                pdcPath,
                "--pdr",
                pdrPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.Success, exitCode);
            Assert.Contains("ok: PDC/PDR XML shape is valid.", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies PDC validation rejects PDR files missing custom feature resources.
    /// </summary>
    [TestMethod]
    public async Task PdcValidateRejectsPdrMissingCustomResources()
    {
        string directory = CreateTestDirectory();
        string pdcPath = Path.Combine(directory, "Printer.pdc.xml");
        string pdrPath = Path.Combine(directory, "Printer.pdr.xml");
        await File.WriteAllTextAsync(pdcPath, ValidCustomPdc, TestContext.CancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(pdrPath, "<root />", TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync(
                "pdc",
                "validate",
                "--pdc",
                pdcPath,
                "--pdr",
                pdrPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.ValidationFailed, exitCode);
            Assert.Contains("PDR is missing resource 'schemas.printsink.dev/printing/keywords/JobWatermarkMode'", output);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies ticket mapping reports IPP attributes produced by Core.
    /// </summary>
    [TestMethod]
    public async Task TicketMapReportsIppAttributes()
    {
        string ticketPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.xml");
        await File.WriteAllTextAsync(ticketPath, ValidPrintTicket, TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync("ticket", "map", "--ticket", ticketPath).ConfigureAwait(false);

            Assert.AreEqual(CliExitCodes.Success, exitCode);
            Assert.Contains("IPP attributes: 5", output);
            Assert.Contains("copies: 2", output);
            Assert.Contains("finishings: 20", output);
            Assert.Contains("output-bin: automationoutputbin", output);
            Assert.Contains("page-delivery: oddpagesthenevenpages", output);
            Assert.Contains("sides: two-sided-short-edge", output);
        }
        finally
        {
            File.Delete(ticketPath);
        }
    }

    /// <summary>
    /// Verifies ticket mapping accepts the shipped operator fixture.
    /// </summary>
    [TestMethod]
    public async Task TicketMapAcceptsShippedFixture()
    {
        string ticketPath = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "fixtures",
            "print-ticket",
            "standard.xml");

        (int exitCode, string output, string error) = await InvokeAsync("ticket", "map", "--ticket", ticketPath)
            .ConfigureAwait(false);

        Assert.AreEqual(CliExitCodes.Success, exitCode);
        Assert.AreEqual(string.Empty, error);
        Assert.Contains("IPP attributes: 5", output);
        Assert.Contains("copies: 2", output);
    }

    private async Task<(int ExitCode, string Output, string Error)> InvokeAsync(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CliApplication
            .RunAsync(args, output, error, TestContext.CancellationToken)
            .ConfigureAwait(false);

        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<(int ExitCode, string Output, string Error)> InvokeQueuesAsync(PrinterQueueSnapshot snapshot)
    {
        return await InvokeQueuesAsync(
                null,
                snapshot,
                (_, _) => Task.FromResult(CliExitCodes.Success))
            .ConfigureAwait(false);
    }

    private async Task<(int ExitCode, string Output, string Error)> InvokeQueuesAsync(
        string? subcommand,
        PrinterQueueSnapshot snapshot,
        Func<string, CancellationToken, Task<int>> runPackageCommand)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        CliContext context = new(output, error, Environment.CurrentDirectory);
        RootCommand rootCommand = new("test root");
        rootCommand.Subcommands.Add(QueuesCommand.Create(context, () => snapshot, runPackageCommand));
        InvocationConfiguration configuration = new()
        {
            Output = output,
            Error = error,
        };
        string[] args = subcommand is null ? ["queues"] : ["queues", subcommand];

        int exitCode = await rootCommand
            .Parse(args)
            .InvokeAsync(configuration, TestContext.CancellationToken)
            .ConfigureAwait(false);

        return (exitCode, output.ToString(), error.ToString());
    }

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PrintSink.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PrintSink.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate PrintSink.slnx.");
    }

    private async Task<string> CreateManifestFixtureAsync(string manifest)
    {
        string directory = CreateTestDirectory();
        string configDirectory = Path.Combine(directory, "Config");
        Directory.CreateDirectory(configDirectory);

        await File.WriteAllTextAsync(Path.Combine(directory, "Package.appxmanifest"), manifest, TestContext.CancellationToken).ConfigureAwait(false);

        foreach (string prefix in new[] { "Pdf", "Xps", "PostScript", "Cloud", "PwgRaster", "Pclm" })
        {
            await File.WriteAllTextAsync(
                Path.Combine(configDirectory, $"Printer{prefix}.pdc.xml"),
                ValidPdc,
                TestContext.CancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(configDirectory, $"Printer{prefix}.pdr.xml"),
                ValidPdr,
                TestContext.CancellationToken).ConfigureAwait(false);
        }

        return directory;
    }

    private const string ValidManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package
          xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
          xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
          xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
          xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
          xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
          xmlns:printsupport="http://schemas.microsoft.com/appx/manifest/printsupport/windows10"
          xmlns:printsupport2="http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2"
          xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
          IgnorableNamespaces="uap uap3 uap10 desktop printsupport printsupport2 rescap">
          <Identity Name="PrintSink" Publisher="CN=PrintSink" Version="1.0.0.0" />
          <Properties>
            <DisplayName>PrintSink</DisplayName>
            <PublisherDisplayName>PrintSink</PublisherDisplayName>
            <Logo>Assets\StoreLogo.png</Logo>
          </Properties>
          <Applications>
            <Application Id="App" Executable="PrintSink.App.exe" EntryPoint="PrintSink.App" uap10:SupportsMultipleInstances="true">
              <uap:VisualElements DisplayName="PrintSink" Description="Virtual printer management" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" BackgroundColor="transparent" />
              <Extensions>
                <uap3:Extension Category="windows.appExecutionAlias">
                  <uap3:AppExecutionAlias>
                    <desktop:ExecutionAlias Alias="printsink-app.exe" />
                  </uap3:AppExecutionAlias>
                </uap3:Extension>
                <printsupport:Extension Category="windows.printSupportWorkflow" EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask" />
                <printsupport:Extension Category="windows.printSupportExtension" EntryPoint="PrintSink.Tasks.PrintSupportExtensionBackgroundTask" />
                <printsupport:Extension Category="windows.printSupportSettingsUI" EntryPoint="PrintSink.App.App" />
                <printsupport:Extension Category="windows.printSupportJobUI" EntryPoint="PrintSink.App.App" />
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PdfPrintDisplayName" PrinterUri="printsink:print-to-pdf" PreferredInputFormat="application/oxps" OutputFileTypes="pdf" PdcFile="Config\PrinterPdf.pdc.xml" PdrFile="Config\PrinterPdf.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:XpsPrintDisplayName" PrinterUri="printsink:print-to-xps" PreferredInputFormat="application/oxps" OutputFileTypes="xps;oxps" PdcFile="Config\PrinterXps.pdc.xml" PdrFile="Config\PrinterXps.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/oxps" MaxVersion="1.0" />
                      <printsupport2:SupportedFormat Type="application/vnd.ms-xpsdocument" MaxVersion="1.0" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PostScriptPrintDisplayName" PrinterUri="printsink:print-to-ps" PreferredInputFormat="application/postscript" OutputFileTypes="ps" PdcFile="Config\PrinterPostScript.pdc.xml" PdrFile="Config\PrinterPostScript.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/postscript" MaxVersion="3.0" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:CloudPrintDisplayName" PrinterUri="printsink:print-to-cloud" PreferredInputFormat="application/oxps" PdcFile="Config\PrinterCloud.pdc.xml" PdrFile="Config\PrinterCloud.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PwgRasterPrintDisplayName" PrinterUri="printsink:print-to-pwgr" PreferredInputFormat="application/oxps" OutputFileTypes="pwgr" PdcFile="Config\PrinterPwgRaster.pdc.xml" PdrFile="Config\PrinterPwgRaster.pdr.xml" />
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PclmPrintDisplayName" PrinterUri="printsink:print-to-pclm" PreferredInputFormat="application/oxps" OutputFileTypes="pclm" PdcFile="Config\PrinterPclm.pdc.xml" PdrFile="Config\PrinterPclm.pdr.xml" />
                </printsupport2:Extension>
              </Extensions>
            </Application>
          </Applications>
          <Capabilities>
            <Capability Name="privateNetworkClientServer" />
            <rescap:Capability Name="runFullTrust" />
          </Capabilities>
          <Extensions>
            <Extension Category="windows.activatableClass.inProcessServer">
              <InProcessServer>
                <Path>WinRT.Host.dll</Path>
                <ActivatableClass ActivatableClassId="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask" ThreadingModel="both" />
                <ActivatableClass ActivatableClassId="PrintSink.Tasks.PrintSupportExtensionBackgroundTask" ThreadingModel="both" />
                <ActivatableClass ActivatableClassId="PrintSink.Tasks.VirtualPrinterBackgroundTask" ThreadingModel="both" />
              </InProcessServer>
            </Extension>
            <Extension Category="windows.activatableClass.inProcessServer">
              <InProcessServer>
                <Path>PrintSink.Xps.dll</Path>
                <ActivatableClass ActivatableClassId="PrintSink.Xps.XpsPageWatermarker" ThreadingModel="both" />
                <ActivatableClass ActivatableClassId="PrintSink.Xps.XpsSequentialDocument" ThreadingModel="both" />
              </InProcessServer>
            </Extension>
          </Extensions>
        </Package>
        """;

    private const string InvalidCloudOutputManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package
          xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
          xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
          xmlns:printsupport="http://schemas.microsoft.com/appx/manifest/printsupport/windows10"
          xmlns:printsupport2="http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2"
          xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
          IgnorableNamespaces="uap printsupport printsupport2 rescap">
          <Identity Name="PrintSink" Publisher="CN=PrintSink" Version="1.0.0.0" />
          <Properties>
            <DisplayName>PrintSink</DisplayName>
            <PublisherDisplayName>PrintSink</PublisherDisplayName>
            <Logo>Assets\StoreLogo.png</Logo>
          </Properties>
          <Applications>
            <Application Id="App" Executable="PrintSink.App.exe" EntryPoint="PrintSink.App">
              <uap:VisualElements DisplayName="PrintSink" Description="Virtual printer management" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" BackgroundColor="transparent" />
              <Extensions>
                <printsupport:Extension Category="windows.printSupportWorkflow" EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask" />
                <printsupport:Extension Category="windows.printSupportExtension" EntryPoint="PrintSink.Tasks.PrintSupportExtensionBackgroundTask" />
                <printsupport:Extension Category="windows.printSupportSettingsUI" EntryPoint="PrintSink.App.App" />
                <printsupport:Extension Category="windows.printSupportJobUI" EntryPoint="PrintSink.App.App" />
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PdfPrintDisplayName" PrinterUri="printsink:print-to-pdf" PreferredInputFormat="application/oxps" OutputFileTypes="pdf" PdcFile="Config\PrinterPdf.pdc.xml" PdrFile="Config\PrinterPdf.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:XpsPrintDisplayName" PrinterUri="printsink:print-to-xps" PreferredInputFormat="application/oxps" OutputFileTypes="xps;oxps" PdcFile="Config\PrinterXps.pdc.xml" PdrFile="Config\PrinterXps.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/oxps" MaxVersion="1.0" />
                      <printsupport2:SupportedFormat Type="application/vnd.ms-xpsdocument" MaxVersion="1.0" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PostScriptPrintDisplayName" PrinterUri="printsink:print-to-ps" PreferredInputFormat="application/postscript" OutputFileTypes="ps" PdcFile="Config\PrinterPostScript.pdc.xml" PdrFile="Config\PrinterPostScript.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/postscript" MaxVersion="3.0" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:CloudPrintDisplayName" PrinterUri="printsink:print-to-cloud" PreferredInputFormat="application/oxps" OutputFileTypes="pdf" PdcFile="Config\PrinterCloud.pdc.xml" PdrFile="Config\PrinterCloud.pdr.xml">
                    <printsupport2:SupportedFormats>
                      <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7" />
                    </printsupport2:SupportedFormats>
                  </printsupport2:PrintSupportVirtualPrinter>
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PwgRasterPrintDisplayName" PrinterUri="printsink:print-to-pwgr" PreferredInputFormat="application/oxps" OutputFileTypes="pwgr" PdcFile="Config\PrinterPwgRaster.pdc.xml" PdrFile="Config\PrinterPwgRaster.pdr.xml" />
                </printsupport2:Extension>
                <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow" EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
                  <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PclmPrintDisplayName" PrinterUri="printsink:print-to-pclm" PreferredInputFormat="application/oxps" OutputFileTypes="pclm" PdcFile="Config\PrinterPclm.pdc.xml" PdrFile="Config\PrinterPclm.pdr.xml" />
                </printsupport2:Extension>
              </Extensions>
            </Application>
          </Applications>
          <Capabilities>
            <Capability Name="privateNetworkClientServer" />
            <rescap:Capability Name="runFullTrust" />
          </Capabilities>
        </Package>
        """;

    private const string ValidPdc = """
        <?xml version="1.0" encoding="utf-8"?>
        <psf2:PrintDeviceCapabilities
          xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
          xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
          <psk:PageOrientation psf2:psftype="Feature">
            <psk:Portrait psf2:psftype="Option" psf2:default="true" />
            <psk:Landscape psf2:psftype="Option" />
          </psk:PageOrientation>
        </psf2:PrintDeviceCapabilities>
        """;

    private const string ValidPdr = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="schemas.printsink.dev/printing/keywords/JobWatermarkMode"><value>Watermark</value></data>
        </root>
        """;

    private const string ValidCustomPdc = """
        <?xml version="1.0" encoding="utf-8"?>
        <psf2:PrintDeviceCapabilities
          xmlns:psf2="http://schemas.microsoft.com/windows/2013/12/printing/printschemaframework2"
          xmlns:printsink="https://schemas.printsink.dev/printing/keywords">
          <printsink:JobWatermarkMode psf2:psftype="Feature">
            <printsink:WatermarkOff psf2:psftype="Option" psf2:default="true" />
          </printsink:JobWatermarkMode>
        </psf2:PrintDeviceCapabilities>
        """;

    private const string ValidCustomPdr = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="schemas.printsink.dev/printing/keywords/JobWatermarkMode"><value>Watermark</value></data>
          <data name="schemas.printsink.dev/printing/keywords/WatermarkOff"><value>Off</value></data>
        </root>
        """;

    private const string ValidPrintTicket = """
        <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                         xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                         xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <psf:Feature name="psk:JobDuplexAllDocumentsContiguously">
            <psf:Option name="psk:TwoSidedShortEdge" />
          </psf:Feature>
          <psf:Feature name="psk:JobOutputBin">
            <psf:Option name="printsink:AutomationOutputBin" />
          </psf:Feature>
          <psf:Feature name="psk:JobPageOrder">
            <psf:Option name="printsink:OddPagesThenEvenPages" />
          </psf:Feature>
          <psf:Feature name="psk:JobStapleAllDocuments">
            <psf:Option name="printsink:StapleUpperLeft" />
          </psf:Feature>
          <psf:ParameterInit name="psk:JobCopiesAllDocuments">
            <psf:Value xsi:type="xsd:integer">2</psf:Value>
          </psf:ParameterInit>
        </psf:PrintTicket>
        """;
}
