using Hex1b;
using Hex1b.Widgets;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tui;

/// <summary>
/// Renders the PrintSink Hex1b diagnostics dashboard.
/// </summary>
internal static class TuiDashboard
{
    /// <summary>
    /// Builds the dashboard widget tree from a preloaded diagnostics model.
    /// </summary>
    /// <param name="context">The Hex1b root context.</param>
    /// <param name="model">The diagnostics model to render.</param>
    /// <returns>The dashboard root widget.</returns>
    public static Hex1bWidget Build(RootContext context, TuiDashboardModel model)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);

        return context.VStack(stack =>
        {
            List<Hex1bWidget> widgets =
            [
                stack.Text("PrintSink"),
                stack.Text("Virtual printer diagnostics"),
                stack.Text($"Queues: {EndpointCatalog.All.Count}"),
                stack.Text(""),
                stack.Text("Validation"),
                stack.Text($"Manifest: {FormatStatus(model.Manifest.Succeeded)} | {TrimPath(model.Manifest.Path)}"),
            ];

            foreach (TuiAssetValidation validation in model.PrintDeviceCapabilities)
            {
                widgets.Add(stack.Text($"{validation.Name} PDC/PDR: {FormatStatus(validation.Succeeded)}"));
            }

            widgets.Add(stack.Text(""));
            widgets.Add(stack.Text("Fixture routes"));
            foreach (TuiRouteCheck routeCheck in model.RouteChecks)
            {
                widgets.Add(stack.Text(
                    string.Concat(
                        routeCheck.QueueName,
                        " | source=",
                        routeCheck.ContentType,
                        " | action=",
                        routeCheck.ActionKind,
                        " | conversion=",
                        routeCheck.ConversionKind?.ToString() ?? "None",
                        " | status=",
                        routeCheck.Status,
                        " | bytes=",
                        routeCheck.OutputBytes)));
            }

            widgets.AddRange(
            [
                stack.Text(""),
                stack.Text("Endpoints"),
            ]);

            foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
            {
                string sink = GetSinkDisplay(endpoint);
                widgets.Add(stack.Text(
                    $"{endpoint.QueueName} | target={endpoint.TargetFormat} | input={endpoint.PreferredInputFormat} | sink={sink}"));
            }

            widgets.Add(stack.Text(""));
            widgets.Add(stack.Text("Commands"));
            widgets.Add(stack.Text("queues | manifest lint | pdc validate | ticket map | sink test"));

            return [.. widgets];
        });
    }

    /// <summary>
    /// Runs the dashboard in a terminal.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the terminal session.</param>
    /// <returns>The terminal exit code.</returns>
    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(Environment.CurrentDirectory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the dashboard in a terminal.
    /// </summary>
    /// <param name="workingDirectory">The working directory used to locate source package assets.</param>
    /// <param name="cancellationToken">The cancellation token for the terminal session.</param>
    /// <returns>The terminal exit code.</returns>
    public static async Task<int> RunAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(context => Build(context, model))
            .Build();

        return await terminal.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GetSinkDisplay(VirtualEndpoint endpoint)
    {
        if (!endpoint.RequiresTargetFile)
        {
            return "custom";
        }

        return endpoint.OutputExtensions.Count == 0
            ? "file"
            : string.Join(",", endpoint.OutputExtensions);
    }

    private static string FormatStatus(bool succeeded)
    {
        return succeeded ? "ok" : "fail";
    }

    private static string TrimPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        return fullPath.StartsWith(currentDirectory, StringComparison.OrdinalIgnoreCase)
            ? fullPath[currentDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
}
