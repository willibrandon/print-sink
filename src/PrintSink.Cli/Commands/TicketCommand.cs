using System.CommandLine;

namespace PrintSink.Cli.Commands;

/// <summary>
/// Creates print-ticket inspection commands.
/// </summary>
internal static class TicketCommand
{
    /// <summary>
    /// Creates the ticket command.
    /// </summary>
    /// <param name="context">The CLI context.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CliContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Command command = new("ticket", "Inspect print-ticket fixtures.");
        command.Subcommands.Add(CreateMapCommand(context));

        return command;
    }

    private static Command CreateMapCommand(CliContext context)
    {
        Option<string> ticketOption = new("--ticket", "-t")
        {
            Description = "Path to a print ticket XML fixture.",
            Required = true,
        };

        Command command = new("map", "Summarize a print-ticket fixture for IPP mapping work.");
        command.Options.Add(ticketOption);
        command.SetAction(parseResult =>
        {
            string ticketPath = parseResult.GetRequiredValue(ticketOption);
            TicketMapResult result = TicketMapper.Map(ticketPath);

            foreach (string message in result.Messages)
            {
                context.Output.WriteLine(message);
            }

            return result.Succeeded ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        });

        return command;
    }
}
