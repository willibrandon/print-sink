using PrintSink.Cli.Tui;
using System.CommandLine;

namespace PrintSink.Cli.Commands;

/// <summary>
/// Creates the Hex1b TUI command.
/// </summary>
internal static class TuiCommand
{
    /// <summary>
    /// Creates the TUI command.
    /// </summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        Command command = new("tui", "Start the Hex1b diagnostics dashboard.");
        command.SetAction(async (_, cancellationToken) =>
            await TuiDashboard.RunAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }
}
