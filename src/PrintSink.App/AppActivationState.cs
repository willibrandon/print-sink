namespace PrintSink.App;

internal static class AppActivationState
{
    private static AppActivationRoute currentRoute = AppActivationRoute.Management(0);

    internal static event Action<AppActivationRoute>? RouteChanged;

    internal static AppActivationRoute CurrentRoute => currentRoute;

    internal static void SetRoute(AppActivationRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        currentRoute = route;
        RouteChanged?.Invoke(route);
    }
}
