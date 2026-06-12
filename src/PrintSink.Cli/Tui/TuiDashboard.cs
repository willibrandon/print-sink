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
    /// Builds the dashboard widget tree.
    /// </summary>
    /// <param name="context">The Hex1b root context.</param>
    /// <returns>The dashboard root widget.</returns>
    public static Hex1bWidget Build(RootContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.VStack(stack =>
        {
            List<Hex1bWidget> widgets =
            [
                stack.Text("PrintSink"),
                stack.Text("Virtual printer diagnostics"),
                stack.Text($"Queues: {EndpointCatalog.All.Count}"),
                stack.Text(""),
                stack.Text("Endpoints"),
            ];

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
        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(Build)
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
}
