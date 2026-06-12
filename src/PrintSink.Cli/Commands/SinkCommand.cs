using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;
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
        Option<string?> outputOption = new("--output", "-o")
        {
            Description = "Optional output path for file-backed endpoints.",
        };

        Command command = new("test", "Resolve a fixture PDL stream through a PrintSink endpoint.");
        command.Options.Add(endpointOption);
        command.Options.Add(contentTypeOption);
        command.Options.Add(inputOption);
        command.Options.Add(outputOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string endpointText = parseResult.GetRequiredValue(endpointOption);
            string contentType = parseResult.GetRequiredValue(contentTypeOption);
            string? inputPath = parseResult.GetValue(inputOption);
            string? outputPath = parseResult.GetValue(outputOption);

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
            if (!endpoint.RequiresTargetFile && !string.IsNullOrWhiteSpace(outputPath))
            {
                context.Error.WriteLine($"Endpoint '{endpoint.QueueName}' is not file-backed and does not accept --output.");
                return CliExitCodes.ValidationFailed;
            }

            CapturingSink cloudSink = new();
            ISink fileSink = new TargetStreamSink();
            EndpointSinkResolver sinkResolver = new(new Dictionary<EndpointKind, ISink>
            {
                [EndpointKind.Pdf] = fileSink,
                [EndpointKind.Xps] = fileSink,
                [EndpointKind.PostScript] = fileSink,
                [EndpointKind.PwgRaster] = fileSink,
                [EndpointKind.Cloud] = cloudSink,
            });
            FixtureVirtualPrinterJob job = new(contentType, endpoint, inputPath, outputPath);
            VirtualPrinterJobProcessor processor = new(new PdlRouter(), new FixturePdlConverter(), sinkResolver);
            VirtualPrinterJobResult result = await processor.ProcessAsync(job, cancellationToken).ConfigureAwait(false);
            PdlPlan plan = result.Plan;

            context.Output.WriteLine($"Endpoint: {endpoint.QueueName}");
            context.Output.WriteLine($"Source: {plan.SourceFormat?.ToString() ?? "Unknown"}");
            context.Output.WriteLine($"Target: {plan.TargetFormat}");
            context.Output.WriteLine($"Action: {plan.ActionKind}");
            context.Output.WriteLine($"Conversion: {plan.ConversionKind?.ToString() ?? "None"}");
            context.Output.WriteLine($"Reason: {plan.Reason}");
            context.Output.WriteLine($"Status: {result.Status}");

            if (!string.IsNullOrWhiteSpace(inputPath))
            {
                context.Output.WriteLine($"InputBytes: {new FileInfo(inputPath).Length}");
            }

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                context.Output.WriteLine($"Output: {outputPath}");
            }

            long outputBytes = endpoint.Kind == EndpointKind.Cloud
                ? cloudSink.BytesWritten
                : job.OutputBytes;
            context.Output.WriteLine($"OutputBytes: {outputBytes}");
            job.DeleteTemporaryOutput();

            return result.Status == Core.Abstractions.VirtualPrinterJobStatus.Succeeded
                ? CliExitCodes.Success
                : CliExitCodes.ValidationFailed;
        });

        return command;
    }
}
