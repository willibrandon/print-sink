using PrintSink.Cli.Tui;
using System.CommandLine;

namespace PrintSink.Cli.Commands;

internal static class TuiCommand
{
    public static Command Create()
    {
        Command command = new("tui", "Start the Hex1b diagnostics dashboard.");
        command.SetAction(async (_, cancellationToken) =>
            await TuiDashboard.RunAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }
}
