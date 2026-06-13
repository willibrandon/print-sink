using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using System.Globalization;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Hosts the PrintSink Reactor shell.
/// </summary>
internal sealed class AppRoot : Component
{
    private static readonly object[] EmptyDependencies = [];

    /// <summary>
    /// Renders the application shell.
    /// </summary>
    /// <returns>The root Reactor element.</returns>
    public override Element Render()
    {
        var (route, setRoute) = UseState(AppActivationState.CurrentRoute);

        UseEffect(() =>
        {
            void OnRouteChanged(AppActivationRoute nextRoute)
            {
                UiDispatch.Post(() => setRoute(nextRoute));
            }

            AppActivationState.RouteChanged += OnRouteChanged;
            return () => AppActivationState.RouteChanged -= OnRouteChanged;
        }, EmptyDependencies);

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            (TitleBar(route.Title) with
            {
                Subtitle = route.Subtitle,
                RightHeader = Caption("MSIX + Reactor")
                    .Foreground(Theme.SecondaryText),
            }).Grid(row: 0),
            RenderRoute(route)
                .Grid(row: 1))
            .Backdrop(BackdropKind.Mica);
    }

    private static ComponentElement RenderRoute(AppActivationRoute route)
    {
        return route.Kind switch
        {
            AppActivationRouteKind.Settings => Component<SettingsScreen, AppActivationRoute>(route)
                .WithKey(route.ActivationId.ToString(CultureInfo.InvariantCulture)),
            AppActivationRouteKind.JobPreview => Component<JobPreviewScreen, AppActivationRoute>(route)
                .WithKey(route.ActivationId.ToString(CultureInfo.InvariantCulture)),
            AppActivationRouteKind.WinRtPrintSource => Component<WinRtPrintSourceScreen, AppActivationRoute>(route)
                .WithKey(route.ActivationId.ToString(CultureInfo.InvariantCulture)),
            _ => Component<ManagementScreen>(),
        };
    }
}
