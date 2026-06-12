using PrintSink.Core.Endpoints;
using System.CommandLine;

namespace PrintSink.Cli.Commands;

/// <summary>
/// Creates queue inspection commands.
/// </summary>
internal static class QueuesCommand
{
    /// <summary>
    /// Creates the queues command.
    /// </summary>
    /// <param name="context">The CLI context.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CliContext context)
    {
        return Create(context, InstalledPrinterReader.Read);
    }

    internal static Command Create(CliContext context, Func<PrinterQueueSnapshot> readInstalledQueues)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(readInstalledQueues);

        Command command = new("queues", "List the PrintSink virtual queues.");
        command.SetAction(_ =>
        {
            PrinterQueueSnapshot installedQueues = readInstalledQueues();
            context.Output.WriteLine("Queue\tTarget\tPreferred\tSink\tInstalled");

            foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
            {
                string sink = GetSinkDisplay(endpoint);
                string installed = GetInstalledStatus(installedQueues, endpoint);
                context.Output.WriteLine(
                    $"{endpoint.QueueName}\t{endpoint.TargetFormat}\t{endpoint.PreferredInputFormat}\t{sink}\t{installed}");
            }

            if (!installedQueues.IsAvailable)
            {
                context.Error.WriteLine($"warning: installed queue status unavailable: {installedQueues.UnavailableReason}");
            }

            return CliExitCodes.Success;
        });

        return command;
    }

    private static string GetInstalledStatus(PrinterQueueSnapshot installedQueues, VirtualEndpoint endpoint)
    {
        if (!installedQueues.IsAvailable)
        {
            return "unknown";
        }

        return installedQueues.Contains(endpoint.QueueName) ? "yes" : "no";
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
