using Hex1b;
using Hex1b.Widgets;
using PrintSink.Core.Diagnostics;
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
        return Build(context, model, static () => { }, static () => { }, static () => { }, static () => { }, "Ready.");
    }

    internal static Hex1bWidget Build(
        RootContext context,
        TuiDashboardModel model,
        Action refreshDashboard,
        string actionStatus)
    {
        return Build(context, model, refreshDashboard, static () => { }, static () => { }, static () => { }, actionStatus);
    }

    internal static Hex1bWidget Build(
        RootContext context,
        TuiDashboardModel model,
        Action refreshDashboard,
        Action installQueues,
        Action removeQueues,
        Action runSinkTests,
        string actionStatus)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(refreshDashboard);
        ArgumentNullException.ThrowIfNull(installQueues);
        ArgumentNullException.ThrowIfNull(removeQueues);
        ArgumentNullException.ThrowIfNull(runSinkTests);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionStatus);

        return context.VStack(stack =>
        {
            List<Hex1bWidget> widgets =
            [
                stack.Text("PrintSink"),
                stack.Text("Virtual printer diagnostics"),
                stack.Text($"Queues: {EndpointCatalog.All.Count}"),
                stack.Text(FormatInstalledQueueSummary(model.InstalledQueues)),
                stack.Text(""),
                stack.Text("Actions"),
                stack.Button("Refresh dashboard").OnClick(_ => refreshDashboard()),
                stack.Button("Install queues").OnClick(_ => installQueues()),
                stack.Button("Remove queues").OnClick(_ => removeQueues()),
                stack.Button("Run sink tests").OnClick(_ => runSinkTests()),
                stack.Text($"Status: {actionStatus}"),
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

            widgets.Add(stack.Text(""));
            widgets.Add(stack.Text("Recent diagnostics"));
            if (model.DiagnosticEvents.Count == 0)
            {
                widgets.Add(stack.Text("No recent diagnostics"));
            }
            else
            {
                foreach (DiagnosticEventRecord diagnosticEvent in model.DiagnosticEvents)
                {
                    widgets.Add(stack.Text(FormatDiagnosticEvent(diagnosticEvent)));
                }
            }

            widgets.AddRange(
            [
                stack.Text(""),
                stack.Text("Endpoints"),
            ]);

            foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
            {
                string sink = GetSinkDisplay(endpoint);
                string installed = GetInstalledStatus(model.InstalledQueues, endpoint);
                widgets.Add(stack.Text(
                    $"{endpoint.QueueName} | target={endpoint.TargetFormat} | input={endpoint.PreferredInputFormat} | sink={sink} | installed={installed}"));
            }

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
        TuiDashboardRuntimeState state = await TuiDashboardRuntimeState
            .CreateAsync(workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                _ => { },
                app =>
                {
                    state.Attach(app);
                    return context => Build(
                        context,
                        state.Model,
                        state.Refresh,
                        state.InstallQueues,
                        state.RemoveQueues,
                        state.RunSinkTests,
                        state.Status);
                })
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

    private static string FormatInstalledQueueSummary(PrinterQueueSnapshot installedQueues)
    {
        if (!installedQueues.IsAvailable)
        {
            return "Installed queues: unknown";
        }

        int installedEndpointCount = EndpointCatalog.All.Count(endpoint => installedQueues.Contains(endpoint.QueueName));
        return $"Installed queues: {installedEndpointCount}/{EndpointCatalog.All.Count}";
    }

    private static string GetInstalledStatus(PrinterQueueSnapshot installedQueues, VirtualEndpoint endpoint)
    {
        if (!installedQueues.IsAvailable)
        {
            return "unknown";
        }

        return installedQueues.Contains(endpoint.QueueName) ? "yes" : "no";
    }

    private static string FormatStatus(bool succeeded)
    {
        return succeeded ? "ok" : "fail";
    }

    private static string FormatDiagnosticEvent(DiagnosticEventRecord diagnosticEvent)
    {
        string endpoint = string.IsNullOrWhiteSpace(diagnosticEvent.Endpoint)
            ? "package"
            : diagnosticEvent.Endpoint;
        string detail = string.IsNullOrWhiteSpace(diagnosticEvent.Detail)
            ? string.Empty
            : $" | {diagnosticEvent.Detail}";

        return string.Concat(
            diagnosticEvent.Timestamp.ToLocalTime().ToString("u", System.Globalization.CultureInfo.InvariantCulture),
            " | ",
            diagnosticEvent.Severity,
            " | ",
            endpoint,
            " | ",
            diagnosticEvent.Message,
            detail);
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
