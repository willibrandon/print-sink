using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Hosts the PrintSink Reactor shell.
/// </summary>
internal sealed class AppRoot : Component
{
    /// <summary>
    /// Renders the application shell.
    /// </summary>
    /// <returns>The root Reactor element.</returns>
    public override Element Render()
    {
        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            (TitleBar("PrintSink") with
            {
                Subtitle = "Virtual printer management",
                RightHeader = Caption("MSIX + Reactor")
                    .Foreground(Theme.SecondaryText),
            }).Grid(row: 0),
            Component<ManagementScreen>()
                .Grid(row: 1))
            .Backdrop(BackdropKind.Mica);
    }
}
