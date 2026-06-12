using System.CommandLine;

namespace PrintSink.Cli.Commands;

/// <summary>
/// Creates PDC inspection commands.
/// </summary>
internal static class PdcCommand
{
    /// <summary>
    /// Creates the PDC command.
    /// </summary>
    /// <param name="context">The CLI context.</param>
    /// <returns>The configured command.</returns>
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
        Option<string?> pdrOption = new("--pdr")
        {
            Description = "Optional path to the matching Print Device Resources XML file.",
        };

        Command command = new("validate", "Validate PDC XML shape and optional PDR resources.");
        command.Options.Add(pdcOption);
        command.Options.Add(pdrOption);
        command.SetAction(parseResult =>
        {
            string pdcPath = parseResult.GetRequiredValue(pdcOption);
            string? pdrPath = parseResult.GetValue(pdrOption);
            ValidationResult result = PdcValidator.Validate(pdcPath, pdrPath);

            foreach (string message in result.Messages)
            {
                context.Output.WriteLine(message);
            }

            return result.Succeeded ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        });

        return command;
    }
}
