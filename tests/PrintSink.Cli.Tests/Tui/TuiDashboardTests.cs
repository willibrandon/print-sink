using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using PrintSink.Cli.Tui;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tests.Tui;

/// <summary>
/// Tests the Hex1b diagnostics dashboard.
/// </summary>
[TestClass]
internal sealed class TuiDashboardTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that the dashboard model validates package assets and fixture routes.
    /// </summary>
    [TestMethod]
    public async Task DashboardModelValidatesAssetsAndRoutes()
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(Environment.CurrentDirectory, TestContext.CancellationToken)
            .ConfigureAwait(false);

        Assert.IsTrue(model.Manifest.Succeeded, string.Join(Environment.NewLine, model.Manifest.Messages));
        Assert.HasCount(EndpointCatalog.All.Count, model.PrintDeviceCapabilities);
        Assert.IsTrue(model.PrintDeviceCapabilities.All(validation => validation.Succeeded));
        Assert.HasCount(EndpointCatalog.All.Count, model.RouteChecks);
        Assert.IsTrue(model.RouteChecks.All(routeCheck => routeCheck.Status == VirtualPrinterJobStatus.Succeeded));
        Assert.IsTrue(model.RouteChecks.All(routeCheck => routeCheck.OutputBytes > 0));
        Assert.IsNotNull(model.DiagnosticEvents);
    }

    /// <summary>
    /// Verifies that the dashboard renders through Hex1b's headless presentation adapter.
    /// </summary>
    [TestMethod]
    public async Task DashboardRendersInHeadlessTerminal()
    {
        string directory = CreateTestDirectory();
        TuiDashboardModel model;
        try
        {
            using LocalDiagnosticEventStore diagnosticEventStore = new(directory);
            await diagnosticEventStore
                .AppendAsync(
                    new DiagnosticEventRecord(
                        DateTimeOffset.UtcNow,
                        DiagnosticEventSeverity.Information,
                        "Test",
                        "Job completed",
                        "PrintSink - PDF",
                        "Succeeded; 12 ms"),
                    TestContext.CancellationToken)
                .ConfigureAwait(false);
            model = await TuiDashboardModel
                .LoadAsync(Environment.CurrentDirectory, diagnosticEventStore, TestContext.CancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => TuiDashboard.Build(context, model))
            .WithHeadless()
            .WithDimensions(120, 50)
            .Build();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        Task<int> runTask = terminal.RunAsync(cancellation.Token);
        using Hex1bTerminalSnapshot snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                screen => screen.ContainsText("PrintSink"),
                TimeSpan.FromSeconds(2),
                "PrintSink dashboard")
            .Ctrl()
            .Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cancellation.Token)
            .ConfigureAwait(false);

        await cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        string screenText = snapshot.GetScreenText();
        Assert.Contains("PrintSink", screenText);
        Assert.Contains("Validation", screenText);
        Assert.Contains("Manifest: ok", screenText);
        Assert.Contains("Pdf PDC/PDR: ok", screenText);
        Assert.Contains("Fixture routes", screenText);
        Assert.Contains("XpsToPdf", screenText);
        Assert.Contains("status=Succeeded", screenText);
        Assert.Contains("Recent diagnostics", screenText);
        Assert.Contains("Job completed", screenText);
        Assert.Contains("PrintSink - PDF", screenText);
        Assert.Contains(".xps,.oxps", screenText);
        Assert.Contains("Actions", screenText);
        Assert.Contains("Refresh dashboard", screenText);
        Assert.Contains("Install queues", screenText);
        Assert.Contains("Remove queues", screenText);
        Assert.Contains("Run sink tests", screenText);
        Assert.Contains("Installed queues:", screenText);
        Assert.Contains("installed=", screenText);
        Assert.Contains("Shell commands", screenText);
        Assert.DoesNotContain("Commands", screenText);
    }

    /// <summary>
    /// Verifies that the dashboard refresh action can be activated from the keyboard.
    /// </summary>
    [TestMethod]
    public async Task DashboardRefreshActionRespondsToEnter()
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(Environment.CurrentDirectory, TestContext.CancellationToken)
            .ConfigureAwait(false);
        bool refreshed = false;

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => TuiDashboard.Build(context, model, () => refreshed = true, "Ready."))
            .WithHeadless()
            .WithDimensions(100, 30)
            .Build();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        Task<int> runTask = terminal.RunAsync(cancellation.Token);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                screen => screen.ContainsText("Refresh dashboard"),
                TimeSpan.FromSeconds(2),
                "Refresh dashboard action")
            .Enter()
            .WaitUntil(
                _ => refreshed,
                TimeSpan.FromSeconds(2),
                "refresh action invocation")
            .Ctrl()
            .Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cancellation.Token)
            .ConfigureAwait(false);

        await cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Verifies that the install queues dashboard action runs the package command from the keyboard.
    /// </summary>
    [TestMethod]
    public async Task DashboardInstallActionRunsPackageCommand()
    {
        await VerifyQueueActionAsync(
                tabCount: 1,
                expectedArgument: "--install-virtual-printers",
                expectedStatus: "Queue install completed.",
                installsQueues: true)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that the remove queues dashboard action runs the package command from the keyboard.
    /// </summary>
    [TestMethod]
    public async Task DashboardRemoveActionRunsPackageCommand()
    {
        await VerifyQueueActionAsync(
                tabCount: 2,
                expectedArgument: "--remove-virtual-printers",
                expectedStatus: "Queue removal completed.",
                installsQueues: false)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that the sink-test dashboard action runs the fixture sink checks from the keyboard.
    /// </summary>
    [TestMethod]
    public async Task DashboardSinkTestActionRunsFixtureChecks()
    {
        TuiDashboardRuntimeState state = await TuiDashboardRuntimeState
            .CreateAsync(
                Environment.CurrentDirectory,
                () => PrinterQueueSnapshot.Available([]),
                (_, _) => Task.FromResult(new TuiPackageCommandResult(CliExitCodes.Success, "ok", string.Empty)),
                TestContext.CancellationToken)
            .ConfigureAwait(false);

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    state.Attach(app);
                    return context => TuiDashboard.Build(
                        context,
                        state.Model,
                        state.Refresh,
                        state.InstallQueues,
                        state.RemoveQueues,
                        state.RunSinkTests,
                        state.Status);
                })
            .WithHeadless()
            .WithDimensions(120, 50)
            .Build();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        Task<int> runTask = terminal.RunAsync(cancellation.Token);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                screen => screen.ContainsText("Run sink tests"),
                TimeSpan.FromSeconds(2),
                "sink test action")
            .Tab()
            .Tab()
            .Tab()
            .Enter()
            .WaitUntil(
                screen => screen.ContainsText($"Fixture sink tests passed for {EndpointCatalog.All.Count} endpoints."),
                TimeSpan.FromSeconds(2),
                "fixture sink test completion")
            .Ctrl()
            .Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cancellation.Token)
            .ConfigureAwait(false);

        await cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PrintSink.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }

    private async Task VerifyQueueActionAsync(
        int tabCount,
        string expectedArgument,
        string expectedStatus,
        bool installsQueues)
    {
        bool queuesInstalled = !installsQueues;
        List<string> packageArguments = [];
        TuiDashboardRuntimeState state = await TuiDashboardRuntimeState
            .CreateAsync(
                Environment.CurrentDirectory,
                () => PrinterQueueSnapshot.Available(GetInstalledQueueNames(queuesInstalled)),
                (argument, _) =>
                {
                    packageArguments.Add(argument);
                    queuesInstalled = installsQueues;
                    return Task.FromResult(new TuiPackageCommandResult(CliExitCodes.Success, "ok", string.Empty));
                },
                TestContext.CancellationToken)
            .ConfigureAwait(false);

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    state.Attach(app);
                    return context => TuiDashboard.Build(
                        context,
                        state.Model,
                        state.Refresh,
                        state.InstallQueues,
                        state.RemoveQueues,
                        state.RunSinkTests,
                        state.Status);
                })
            .WithHeadless()
            .WithDimensions(120, 50)
            .Build();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        Task<int> runTask = terminal.RunAsync(cancellation.Token);
        Hex1bTerminalInputSequenceBuilder sequenceBuilder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                screen => screen.ContainsText("Install queues") && screen.ContainsText("Remove queues"),
                TimeSpan.FromSeconds(2),
                "queue actions");
        for (int index = 0; index < tabCount; index++)
        {
            sequenceBuilder.Tab();
        }

        await sequenceBuilder
            .Enter()
            .WaitUntil(
                screen => screen.ContainsText(expectedStatus),
                TimeSpan.FromSeconds(2),
                expectedStatus)
            .Ctrl()
            .Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cancellation.Token)
            .ConfigureAwait(false);

        await cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains(expectedArgument, packageArguments);
    }

    private static string[] GetInstalledQueueNames(bool queuesInstalled)
    {
        return queuesInstalled
            ? [.. EndpointCatalog.All.Select(endpoint => endpoint.QueueName)]
            : [];
    }
}
