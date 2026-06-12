using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using PrintSink.Cli.Tui;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tests.Tui;

/// <summary>
/// Tests the Hex1b diagnostics dashboard.
/// </summary>
[TestClass]
public sealed class TuiDashboardTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that the dashboard model validates package assets and fixture routes.
    /// </summary>
    [TestMethod]
    public async Task Dashboard_model_validates_assets_and_routes()
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
    }

    /// <summary>
    /// Verifies that the dashboard renders through Hex1b's headless presentation adapter.
    /// </summary>
    [TestMethod]
    public async Task Dashboard_renders_in_headless_terminal()
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(Environment.CurrentDirectory, TestContext.CancellationToken)
            .ConfigureAwait(false);
        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => TuiDashboard.Build(context, model))
            .WithHeadless()
            .WithDimensions(100, 30)
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

        cancellation.Cancel();

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
        Assert.Contains("PrintSink - PDF", screenText);
        Assert.Contains(".xps,.oxps", screenText);
    }
}
