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
        });

        return command;
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
