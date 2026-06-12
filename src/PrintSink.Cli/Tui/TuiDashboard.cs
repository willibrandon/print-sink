using Hex1b;
using Hex1b.Widgets;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tui;

internal static class TuiDashboard
{
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
                string sink = endpoint.RequiresTargetFile ? endpoint.DefaultExtension ?? "file" : "custom";
                widgets.Add(stack.Text(
                    $"{endpoint.QueueName} | target={endpoint.TargetFormat} | input={endpoint.PreferredInputFormat} | sink={sink}"));
            }

            widgets.Add(stack.Text(""));
            widgets.Add(stack.Text("Commands"));
            widgets.Add(stack.Text("queues | manifest lint | pdc validate | ticket map | sink test"));

            return [.. widgets];
        });
    }

    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using Hex1bTerminal terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(Build)
            .Build();

        return await terminal.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
