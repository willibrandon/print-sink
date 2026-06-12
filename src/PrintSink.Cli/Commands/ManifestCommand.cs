using System.CommandLine;

namespace PrintSink.Cli.Commands;

internal static class ManifestCommand
{
    public static Command Create(CliContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Command command = new("manifest", "Inspect the MSIX package manifest.");
        command.Subcommands.Add(CreateLintCommand(context));

        return command;
    }

    private static Command CreateLintCommand(CliContext context)
    {
        Option<string> manifestOption = new("--manifest", "-m")
        {
            Description = "Path to Package.appxmanifest.",
            DefaultValueFactory = _ => Path.Combine(
                context.WorkingDirectory,
                "src",
                "PrintSink.App",
                "Package.appxmanifest"),
        };

        Command command = new("lint", "Validate PrintSink package manifest shape.");
        command.Options.Add(manifestOption);
        command.SetAction(parseResult =>
        {
            string manifestPath = parseResult.GetRequiredValue(manifestOption);
            ManifestLintResult result = ManifestLinter.Lint(manifestPath);

            foreach (string message in result.Messages)
            {
                context.Output.WriteLine(message);
            }

            return result.Succeeded ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        });

        return command;
    }
}
