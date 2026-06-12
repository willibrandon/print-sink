using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using System.CommandLine;

namespace PrintSink.Cli.Commands;

/// <summary>
/// Creates sink test commands.
/// </summary>
internal static class SinkCommand
{
    /// <summary>
    /// Creates the sink command.
    /// </summary>
    /// <param name="context">The CLI context.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CliContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Command command = new("sink", "Exercise sink routing without print activation.");
        command.Subcommands.Add(CreateTestCommand(context));

        return command;
    }

    private static Command CreateTestCommand(CliContext context)
    {
        Option<string> endpointOption = new("--endpoint", "-e")
        {
            Description = "Endpoint kind: pdf, xps, postscript, cloud, or pwg-raster.",
            Required = true,
        };
        Option<string> contentTypeOption = new("--content-type", "-c")
        {
            Description = "Source PDL content type.",
            Required = true,
        };
        Option<string?> inputOption = new("--input", "-i")
        {
            Description = "Optional fixture PDL file path.",
        };

        Command command = new("test", "Resolve a fixture PDL stream through a PrintSink endpoint.");
        command.Options.Add(endpointOption);
        command.Options.Add(contentTypeOption);
        command.Options.Add(inputOption);
        command.SetAction(parseResult =>
        {
            string endpointText = parseResult.GetRequiredValue(endpointOption);
            string contentType = parseResult.GetRequiredValue(contentTypeOption);
            string? inputPath = parseResult.GetValue(inputOption);

            if (!EndpointParser.TryParse(endpointText, out EndpointKind endpointKind))
            {
                context.Error.WriteLine($"Unknown endpoint '{endpointText}'.");
                return CliExitCodes.UsageError;
            }

            if (!string.IsNullOrWhiteSpace(inputPath) && !File.Exists(inputPath))
            {
                context.Error.WriteLine($"Input file not found: {inputPath}");
                return CliExitCodes.ValidationFailed;
            }

            VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
            PdlPlan plan = new PdlRouter().Resolve(contentType, endpoint);

            context.Output.WriteLine($"Endpoint: {endpoint.QueueName}");
            context.Output.WriteLine($"Source: {plan.SourceFormat?.ToString() ?? "Unknown"}");
            context.Output.WriteLine($"Target: {plan.TargetFormat}");
            context.Output.WriteLine($"Action: {plan.ActionKind}");
            context.Output.WriteLine($"Conversion: {plan.ConversionKind?.ToString() ?? "None"}");
            context.Output.WriteLine($"Reason: {plan.Reason}");

            if (!string.IsNullOrWhiteSpace(inputPath))
            {
                context.Output.WriteLine($"InputBytes: {new FileInfo(inputPath).Length}");
            }

            return plan.ActionKind == PdlActionKind.Reject
                ? CliExitCodes.ValidationFailed
                : CliExitCodes.Success;
        });

        return command;
    }
}
