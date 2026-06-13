using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Windows.Graphics.Printing;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace PrintSink.App;

/// <summary>
/// Hosts the WinRT print-source harness used by real E2E print automation.
/// </summary>
internal sealed class WinRtPrintSourceScreen : Component<AppActivationRoute>
{
    private static readonly object NoWindowDependency = new();

    /// <summary>
    /// Renders the WinRT print-source harness.
    /// </summary>
    /// <returns>The root Reactor element.</returns>
    public override Element Render()
    {
        ReactorWindow? window = UseWindow();
        object windowDependency = window ?? NoWindowDependency;
        var (started, setStarted) = UseState(false);
        var (status, setStatus) = UseState("Preparing the Windows print source.");

        UseEffect(() =>
        {
            if (window is null || started)
            {
                return;
            }

            setStarted(true);
            _ = ShowPrintDialogAsync(
                window.NativeWindow,
                Props.WinRtSourceText ?? "foo",
                setStatus);
        }, windowDependency, started);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            VStack(12,
                TextBlock("WinRT print source")
                    .FontSize(28)
                    .Bold(),
                TextBlock(status)
                    .Foreground(SecondaryText),
                TextBlock(Props.WinRtSourceText ?? "foo")
                    .FontSize(20)
                    .FontFamily("Consolas")))
            .MaxWidth(520)
            .VAlign(VerticalAlignment.Center)
            .HAlign(HorizontalAlignment.Center)
            .Padding(32);
    }

    private static async Task ShowPrintDialogAsync(
        Window window,
        string sourceText,
        Action<string> setStatus)
    {
        try
        {
            setStatus("Opening the Windows print dialog.");
            WinRtPrintSourceSession session = new(sourceText);
            PrintTaskCompletion completion = await session.ShowAsync(window).ConfigureAwait(true);
            setStatus(completion switch
            {
                PrintTaskCompletion.Submitted => "The WinRT print task was submitted.",
                PrintTaskCompletion.Canceled => "The WinRT print task was canceled.",
                PrintTaskCompletion.Failed => "The WinRT print task failed.",
                _ => "The WinRT print task closed.",
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            setStatus("The WinRT print source failed.");
            VirtualPrinterCommandLine.WriteDiagnostic($"WinRT print source failed: {ex}");
        }
        finally
        {
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
            Application.Current.Exit();
        }
    }
}
