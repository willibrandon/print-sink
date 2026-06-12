using PrintSink.Cli.Commands;
using System.CommandLine;

namespace PrintSink.Cli;

internal static class CliApplication
{
    public static Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CliContext context = new(output, error, Environment.CurrentDirectory);
        RootCommand rootCommand = CreateRootCommand(context);
        InvocationConfiguration configuration = new()
        {
            Output = output,
            Error = error,
        };

        return rootCommand.Parse(args).InvokeAsync(configuration, cancellationToken);
    }

    private static RootCommand CreateRootCommand(CliContext context)
    {
        RootCommand rootCommand = new("PrintSink developer and operator tooling.");

        rootCommand.Subcommands.Add(QueuesCommand.Create(context));
        rootCommand.Subcommands.Add(ManifestCommand.Create(context));
        rootCommand.Subcommands.Add(PdcCommand.Create(context));
        rootCommand.Subcommands.Add(TicketCommand.Create(context));
        rootCommand.Subcommands.Add(SinkCommand.Create(context));
        rootCommand.Subcommands.Add(TuiCommand.Create());

        return rootCommand;
    }
}
