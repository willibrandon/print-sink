using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using PrintSink.Cli.Tui;

namespace PrintSink.Cli.Tests.Tui;

/// <summary>
/// Tests the Hex1b diagnostics dashboard.
/// </summary>
[TestClass]
public sealed class TuiDashboardTests
{
    /// <summary>
    /// Verifies that the dashboard renders through Hex1b's headless presentation adapter.
    /// </summary>
    [TestMethod]
    public async Task Dashboard_renders_in_headless_terminal()
    {
        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(TuiDashboard.Build)
            .WithHeadless()
            .WithDimensions(100, 30)
            .Build();
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));

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
        Assert.Contains("PrintSink - PDF", screenText);
    }
}
