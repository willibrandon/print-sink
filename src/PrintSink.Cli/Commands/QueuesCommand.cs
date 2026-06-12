using PrintSink.Core.Endpoints;
using System.CommandLine;
using System.Text;

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
        return Create(
            context,
            InstalledPrinterReader.Read,
            (argument, cancellationToken) => AppPackageCommandRunner.RunAsync(
                argument,
                context.Output,
                context.Error,
                cancellationToken));
    }

    internal static Command Create(CliContext context, Func<PrinterQueueSnapshot> readInstalledQueues)
    {
        return Create(context, readInstalledQueues, (_, _) => Task.FromResult(CliExitCodes.Success));
    }

    internal static Command Create(
        CliContext context,
        Func<PrinterQueueSnapshot> readInstalledQueues,
        Func<string, CancellationToken, Task<int>> runPackageCommand)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(readInstalledQueues);
        ArgumentNullException.ThrowIfNull(runPackageCommand);

        Command command = new("queues", "List the PrintSink virtual queues.");
        command.SetAction(_ => WriteQueues(context, readInstalledQueues()));
        command.Subcommands.Add(CreateProvisionCommand(
            "install",
            "Install the PrintSink virtual printer queues through the packaged app.",
            "--install-virtual-printers",
            context,
            readInstalledQueues,
            runPackageCommand));
        command.Subcommands.Add(CreateProvisionCommand(
            "remove",
            "Remove the PrintSink virtual printer queues through the packaged app.",
            "--remove-virtual-printers",
            context,
            readInstalledQueues,
            runPackageCommand));

        return command;
    }

    private static Command CreateProvisionCommand(
        string name,
        string description,
        string packageArgument,
        CliContext context,
        Func<PrinterQueueSnapshot> readInstalledQueues,
        Func<string, CancellationToken, Task<int>> runPackageCommand)
    {
        Command command = new(name, description);
        command.SetAction(async (_, cancellationToken) =>
        {
            int exitCode = await runPackageCommand(packageArgument, cancellationToken).ConfigureAwait(false);
            if (exitCode != CliExitCodes.Success)
            {
                context.Error.WriteLine($"Package command failed with exit code {exitCode}.");
                return exitCode;
            }

            context.Output.WriteLine($"{name} completed.");
            return WriteQueues(context, readInstalledQueues());
        });

        return command;
    }

    private static int WriteQueues(CliContext context, PrinterQueueSnapshot installedQueues)
    {
        string[][] rows = [.. EndpointCatalog.All.Select(endpoint => new[]
        {
            endpoint.QueueName,
            endpoint.TargetFormat.ToString(),
            endpoint.PreferredInputFormat.ToString(),
            GetSinkDisplay(endpoint),
            GetInstalledStatus(installedQueues, endpoint),
        })];
        WriteTable(context.Output, ["Queue", "Target", "Preferred", "Sink", "Installed"], rows);

        if (!installedQueues.IsAvailable)
        {
            context.Error.WriteLine($"warning: installed queue status unavailable: {installedQueues.UnavailableReason}");
        }

        return CliExitCodes.Success;
    }

    private static void WriteTable(TextWriter output, string[] headings, string[][] rows)
    {
        int[] widths = GetColumnWidths(headings, rows);
        output.WriteLine(FormatRow(headings, widths));
        output.WriteLine(FormatRow([.. headings.Select((heading, index) => new string('-', Math.Max(heading.Length, widths[index])))], widths));

        foreach (string[] row in rows)
        {
            output.WriteLine(FormatRow(row, widths));
        }
    }

    private static int[] GetColumnWidths(string[] headings, string[][] rows)
    {
        int[] widths = [.. headings.Select(heading => heading.Length)];
        foreach (string[] row in rows)
        {
            for (int index = 0; index < row.Length; index++)
            {
                widths[index] = Math.Max(widths[index], row[index].Length);
            }
        }

        return widths;
    }

    private static string FormatRow(string[] cells, int[] widths)
    {
        StringBuilder row = new();
        for (int index = 0; index < cells.Length; index++)
        {
            if (index > 0)
            {
                row.Append("  ");
            }

            row.Append(index == cells.Length - 1 ? cells[index] : cells[index].PadRight(widths[index]));
        }

        return row.ToString();
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
