extern alias PrintSinkApp;

using System.Xml.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;
using Windows.Storage;

namespace PrintSink.App.Tests;

/// <summary>
/// Tests package-hosted app behavior.
/// </summary>
[TestClass]
internal sealed class AppPackageTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies the management activation route exposes the shell metadata used at normal launch.
    /// </summary>
    [TestMethod]
    public void ManagementRouteUsesManagementShellMetadata()
    {
        PrintSinkApp::PrintSink.App.AppActivationRoute route =
            PrintSinkApp::PrintSink.App.AppActivationRoute.Management(42);

        Assert.AreEqual(42, route.ActivationId);
        Assert.AreEqual(PrintSinkApp::PrintSink.App.AppActivationRouteKind.Management, route.Kind);
        Assert.AreEqual("PrintSink", route.Title);
        Assert.AreEqual("Virtual printer management", route.Subtitle);
        Assert.IsNull(route.SettingsArgs);
        Assert.IsNull(route.JobArgs);
    }

    /// <summary>
    /// Verifies the WinRT source route carries the source text used by the print harness.
    /// </summary>
    [TestMethod]
    public void WinRtSourceRouteUsesPrintSourceMetadata()
    {
        PrintSinkApp::PrintSink.App.AppActivationRoute route =
            PrintSinkApp::PrintSink.App.AppActivationRoute.WinRtPrintSource(42, "foo");

        Assert.AreEqual(42, route.ActivationId);
        Assert.AreEqual(PrintSinkApp::PrintSink.App.AppActivationRouteKind.WinRtPrintSource, route.Kind);
        Assert.AreEqual("WinRT print source", route.Title);
        Assert.AreEqual("Windows print pipeline", route.Subtitle);
        Assert.AreEqual("foo", route.WinRtSourceText);
        Assert.IsNull(route.SettingsArgs);
        Assert.IsNull(route.JobArgs);
    }

    /// <summary>
    /// Verifies the packaged test host owns a usable XAML UI thread.
    /// </summary>
    [UITestMethod]
    public void XamlRuntimeIsAvailableInsidePackagedTestHost()
    {
        Grid grid = new();

        Assert.AreEqual(0, grid.MinWidth);
    }

    /// <summary>
    /// Verifies settings activation owner IDs are copied across the Windows and Microsoft UI projections.
    /// </summary>
    [TestMethod]
    public void SettingsWindowOwnerConvertsOwnerWindowIdProjection()
    {
        Windows.UI.WindowId source;
        source.Value = 123;

        Microsoft.UI.WindowId actual =
            PrintSinkApp::PrintSink.App.SettingsWindowOwner.ToMicrosoftWindowId(source);

        Assert.AreEqual(source.Value, actual.Value);
    }

    /// <summary>
    /// Verifies app settings resolve under the package-local storage root.
    /// </summary>
    [TestMethod]
    public void AppSettingsStoreUsesPackageLocalSettingsDirectory()
    {
        string localFolderPath = ApplicationData.Current.LocalFolder.Path;
        string expected = PackagedSettingsDirectory.GetRootDirectory(localFolderPath);
        string actual = PrintSinkApp::PrintSink.App.AppSettingsStoreFactory.GetRootDirectory();

        Assert.AreEqual(expected, actual);
        Assert.IsTrue(actual.StartsWith(localFolderPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies loose development packages report why virtual-printer provisioning is blocked.
    /// </summary>
    [TestMethod]
    public void VirtualPrinterProvisioningReportsDevelopmentModePackage()
    {
        string packageRoot = Path.Combine(
            Path.GetTempPath(),
            "PrintSink",
            "src",
            "PrintSink.App",
            "bin",
            "x64",
            "Debug",
            "net10.0-windows10.0.26100.0",
            "AppX");
        string? blockerMessage = PrintSinkApp::PrintSink.App.VirtualPrinterInstaller.GetProvisioningBlockerMessage(packageRoot);

        Assert.IsNotNull(blockerMessage);
        Assert.Contains("loose development layout", blockerMessage);
        Assert.Contains("signed MSIX", blockerMessage);
    }

    /// <summary>
    /// Verifies virtual-printer removal leaves the default printer alone when it is not a PrintSink queue.
    /// </summary>
    [TestMethod]
    public void ChooseReplacementDefaultPrinterReturnsNullForExternalDefault()
    {
        HashSet<string> printSinkPrinters = new(StringComparer.OrdinalIgnoreCase)
        {
            "PrintSink - PDF",
        };
        string? replacement = PrintSinkApp::PrintSink.App.VirtualPrinterInstaller
            .ChooseReplacementDefaultPrinter(
                "Microsoft Print to PDF",
                printSinkPrinters,
                ["Microsoft Print to PDF", "PrintSink - PDF"]);

        Assert.IsNull(replacement);
    }

    /// <summary>
    /// Verifies virtual-printer removal can move the default printer away from PrintSink.
    /// </summary>
    [TestMethod]
    public void ChooseReplacementDefaultPrinterReturnsFirstExternalPrinterForPrintSinkDefault()
    {
        HashSet<string> printSinkPrinters = new(StringComparer.OrdinalIgnoreCase)
        {
            "PrintSink - PDF",
            "PrintSink - XPS",
        };
        string? replacement = PrintSinkApp::PrintSink.App.VirtualPrinterInstaller
            .ChooseReplacementDefaultPrinter(
                "printsink - pdf",
                printSinkPrinters,
                ["PrintSink - PDF", "Microsoft Print to PDF", "PrintSink - XPS"]);

        Assert.AreEqual("Microsoft Print to PDF", replacement);
    }

    /// <summary>
    /// Verifies headless activation argument parsing ignores missing or whitespace-only payloads.
    /// </summary>
    [TestMethod]
    public void SplitArgumentsHandlesEmptyActivationPayloads()
    {
        string[] nullActual = PrintSinkApp::PrintSink.App.VirtualPrinterCommandLine.SplitArguments(null);
        string[] whitespaceActual = PrintSinkApp::PrintSink.App.VirtualPrinterCommandLine.SplitArguments("   ");

        CollectionAssert.AreEqual(Array.Empty<string>(), nullActual);
        CollectionAssert.AreEqual(Array.Empty<string>(), whitespaceActual);
    }

    /// <summary>
    /// Verifies headless activation argument parsing preserves quoted values.
    /// </summary>
    [TestMethod]
    public void SplitArgumentsPreservesQuotedActivationValues()
    {
        string arguments = "--install-virtual-printers --log \"C:\\Temp\\Print Sink\\headless.log\"";
        string[] expected =
        [
            "--install-virtual-printers",
            "--log",
            "C:\\Temp\\Print Sink\\headless.log",
        ];

        string[] actual = PrintSinkApp::PrintSink.App.VirtualPrinterCommandLine.SplitArguments(arguments);

        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies headless activation argument parsing follows Windows quote escaping rules.
    /// </summary>
    [TestMethod]
    public void SplitArgumentsPreservesEscapedQuotesAndEmptyValues()
    {
        string arguments = "--name \"Print \\\"Sink\\\"\" \"\"";
        string[] expected =
        [
            "--name",
            "Print \"Sink\"",
            string.Empty,
        ];

        string[] actual = PrintSinkApp::PrintSink.App.VirtualPrinterCommandLine.SplitArguments(arguments);

        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies headless activation argument parsing preserves doubled trailing backslashes.
    /// </summary>
    [TestMethod]
    public void SplitArgumentsPreservesTrailingBackslashesBeforeClosingQuotes()
    {
        string arguments = "--path \"C:\\Temp\\Print Sink\\\\\"";
        string[] expected =
        [
            "--path",
            "C:\\Temp\\Print Sink\\",
        ];

        string[] actual = PrintSinkApp::PrintSink.App.VirtualPrinterCommandLine.SplitArguments(arguments);

        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies management default-copy input is normalized to the supported ticket range.
    /// </summary>
    [TestMethod]
    public void ManagementDefaultCopiesInputIsNormalized()
    {
        Assert.AreEqual(1, PrintSinkApp::PrintSink.App.ManagementScreen.NormalizeCopies(double.NaN));
        Assert.AreEqual(1, PrintSinkApp::PrintSink.App.ManagementScreen.NormalizeCopies(-10));
        Assert.AreEqual(2, PrintSinkApp::PrintSink.App.ManagementScreen.NormalizeCopies(1.5));
        Assert.AreEqual(999, PrintSinkApp::PrintSink.App.ManagementScreen.NormalizeCopies(1000));
    }

    /// <summary>
    /// Verifies print-ticket XML copy edits add the required parameter initializer.
    /// </summary>
    [TestMethod]
    public void UserDefaultPrintTicketEditorAddsCopiesInitializer()
    {
        XDocument document = XDocument.Parse(
            """
            <psf:PrintTicket
                xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords" />
            """);

        PrintSinkApp::PrintSink.App.UserDefaultPrintTicketEditor.SetCopies(document, 7);

        Assert.AreEqual(7, PrintSinkApp::PrintSink.App.UserDefaultPrintTicketEditor.ReadCopies(document));
        Assert.Contains("JobCopiesAllDocuments", document.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>
    /// Verifies print-ticket XML copy edits update an existing parameter initializer.
    /// </summary>
    [TestMethod]
    public void UserDefaultPrintTicketEditorUpdatesCopiesInitializer()
    {
        XDocument document = XDocument.Parse(
            """
            <psf:PrintTicket
                xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <psf:ParameterInit name="psk:JobCopiesAllDocuments">
                <psf:Value xsi:type="xsd:integer">3</psf:Value>
              </psf:ParameterInit>
            </psf:PrintTicket>
            """);

        PrintSinkApp::PrintSink.App.UserDefaultPrintTicketEditor.SetCopies(document, 4);

        Assert.AreEqual(4, PrintSinkApp::PrintSink.App.UserDefaultPrintTicketEditor.ReadCopies(document));
    }

    /// <summary>
    /// Verifies foreground job options are visible through package-backed local settings and consumed once.
    /// </summary>
    [TestMethod]
    public async Task AppSettingsStoreRoundTripsJobOptionsInsidePackageIdentity()
    {
        string rootDirectory = ResetPackagedSettingsDirectory();
        LocalSettingsStore store = PrintSinkApp::PrintSink.App.AppSettingsStoreFactory.Create();
        JobPasswordOptions passwordOptions = JobPasswordOptions.FromPassword("package-secret", "sha2-256");
        JobProcessingOptions expected = new(
            new WatermarkOptions(
                true,
                new TextWatermark("Package job", "Segoe UI", 36, 0.4, -25, 0, 0),
                null),
            passwordOptions);

        try
        {
            await store
                .SaveJobProcessingOptionsAsync(expected, TestContext.CancellationToken)
                .ConfigureAwait(false);

            JobProcessingOptions? actual = await store
                .ConsumeJobProcessingOptionsAsync(TestContext.CancellationToken)
                .ConfigureAwait(false);
            JobProcessingOptions? missing = await store
                .ConsumeJobProcessingOptionsAsync(TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsNotNull(actual);
            Assert.IsTrue(actual.WatermarkOptions.Enabled);
            Assert.AreEqual("Package job", actual.WatermarkOptions.Text?.Text);
            Assert.IsNotNull(actual.JobPasswordOptions);
            Assert.AreEqual("sha2-256", actual.JobPasswordOptions.EncryptionMethod);
            CollectionAssert.AreEqual(
                passwordOptions.GetEncryptedPassword(),
                actual.JobPasswordOptions.GetEncryptedPassword());
            Assert.IsNull(missing);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    /// <summary>
    /// Verifies packaged job UI options round-trip through package-backed local settings.
    /// </summary>
    [TestMethod]
    public async Task AppSettingsStoreRoundTripsJobUiOptionsInsidePackageIdentity()
    {
        string rootDirectory = ResetPackagedSettingsDirectory();
        LocalSettingsStore store = PrintSinkApp::PrintSink.App.AppSettingsStoreFactory.Create();

        try
        {
            await store
                .SaveJobUiOptionsAsync(new JobUiOptions(false), TestContext.CancellationToken)
                .ConfigureAwait(false);

            JobUiOptions actual = await store
                .GetJobUiOptionsAsync(TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsFalse(actual.LaunchJobUi);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    /// <summary>
    /// Verifies packaged diagnostics round-trip through package-backed local storage.
    /// </summary>
    [TestMethod]
    public async Task AppDiagnosticStoreRoundTripsEventsInsidePackageIdentity()
    {
        string rootDirectory = ResetPackagedSettingsDirectory();
        using LocalDiagnosticEventStore store = PrintSinkApp::PrintSink.App.AppSettingsStoreFactory.CreateDiagnosticEventStore();
        DiagnosticEventRecord expected = new(
            DateTimeOffset.UtcNow,
            DiagnosticEventSeverity.Information,
            "Test",
            "Job completed",
            "PrintSink - PDF",
            "Succeeded; 10 ms");

        try
        {
            await store
                .AppendAsync(expected, TestContext.CancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<DiagnosticEventRecord> actual = await store
                .ReadRecentAsync(4, TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.HasCount(1, actual);
            Assert.AreEqual("Job completed", actual[0].Message);
            Assert.AreEqual("PrintSink - PDF", actual[0].Endpoint);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    private static string ResetPackagedSettingsDirectory()
    {
        string rootDirectory = PrintSinkApp::PrintSink.App.AppSettingsStoreFactory.GetRootDirectory();
        DeleteDirectory(rootDirectory);
        return rootDirectory;
    }

    private static void DeleteDirectory(string rootDirectory)
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, true);
        }
    }
}
