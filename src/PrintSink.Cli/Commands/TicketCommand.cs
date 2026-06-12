using System.CommandLine;

namespace PrintSink.Cli.Commands;

internal static class TicketCommand
{
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
