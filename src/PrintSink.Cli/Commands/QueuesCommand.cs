using PrintSink.Core.Endpoints;
using System.CommandLine;

namespace PrintSink.Cli.Commands;

internal static class QueuesCommand
{
    public static Command Create(CliContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Command command = new("queues", "List the PrintSink virtual queues.");
        command.SetAction(_ =>
        {
            context.Output.WriteLine("Queue\tTarget\tPreferred\tSink");

            foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
            {
                string sink = endpoint.RequiresTargetFile ? endpoint.DefaultExtension ?? "file" : "custom";
                context.Output.WriteLine(
                    $"{endpoint.QueueName}\t{endpoint.TargetFormat}\t{endpoint.PreferredInputFormat}\t{sink}");
            }

            return CliExitCodes.Success;
        });

        return command;
    }
}
