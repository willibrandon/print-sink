using PrintSink.Cli;

namespace PrintSink.Cli.Tests.Commands;

/// <summary>
/// Tests the command-line surface.
/// </summary>
[TestClass]
public sealed class CliApplicationTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that the queue command prints the built-in endpoint list.
    /// </summary>
    [TestMethod]
    public async Task Queues_writes_builtin_endpoint_names()
    {
        (int exitCode, string output, _) = await InvokeAsync("queues").ConfigureAwait(false);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("PrintSink - PDF", output);
        Assert.Contains("PrintSink - PWG Raster", output);
    }

    /// <summary>
    /// Verifies that sink testing uses the core PDL router.
    /// </summary>
    [TestMethod]
    public async Task Sink_test_reports_conversion_route()
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
    /// Verifies manifest linting against a packaged virtual-printer manifest fixture.
    /// </summary>
    [TestMethod]
    public async Task Manifest_lint_accepts_virtual_printer_manifest()
    {
        string manifestPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.appxmanifest");
        await File.WriteAllTextAsync(manifestPath, ValidManifest, TestContext.CancellationToken).ConfigureAwait(false);

        try
        {
            (int exitCode, string output, _) = await InvokeAsync("manifest", "lint", "--manifest", manifestPath).ConfigureAwait(false);

            Assert.AreEqual(0, exitCode);
            Assert.Contains("ok: manifest package shape is valid.", output);
        }
        finally
        {
            File.Delete(manifestPath);
        }
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

    private const string ValidManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package
          xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
          xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
          xmlns:uap11="http://schemas.microsoft.com/appx/manifest/uap/windows10/11"
          xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
          IgnorableNamespaces="uap uap11 rescap">
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
                <uap11:Extension Category="windows.printSupportVirtualPrinterWorkflow" />
                <uap11:Extension Category="windows.printSupportExtension" />
                <uap11:Extension Category="windows.printSupportSettingsUI" />
                <uap11:Extension Category="windows.printSupportJobUI" />
              </Extensions>
            </Application>
          </Applications>
          <Capabilities>
            <rescap:Capability Name="runFullTrust" />
          </Capabilities>
        </Package>
        """;
}
