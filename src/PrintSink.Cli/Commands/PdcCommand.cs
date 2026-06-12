using System.CommandLine;

namespace PrintSink.Cli.Commands;

internal static class PdcCommand
{
    public static Command Create(CliContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Command command = new("pdc", "Inspect print device capability XML.");
        command.Subcommands.Add(CreateValidateCommand(context));

        return command;
    }

    private static Command CreateValidateCommand(CliContext context)
    {
        Option<string> pdcOption = new("--pdc", "-p")
        {
            Description = "Path to a Print Device Capabilities XML file.",
            Required = true,
        };

        Command command = new("validate", "Validate basic PDC XML shape.");
        command.Options.Add(pdcOption);
        command.SetAction(parseResult =>
        {
            string pdcPath = parseResult.GetRequiredValue(pdcOption);
            ValidationResult result = PdcValidator.Validate(pdcPath);

            foreach (string message in result.Messages)
            {
                context.Output.WriteLine(message);
            }

            return result.Succeeded ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        });

        return command;
    }
}
