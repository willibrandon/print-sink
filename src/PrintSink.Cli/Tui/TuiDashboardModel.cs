using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Processing;

namespace PrintSink.Cli.Tui;

internal sealed class TuiDashboardModel
{
    private TuiDashboardModel(
        TuiAssetValidation manifest,
        TuiAssetValidation[] printDeviceCapabilities,
        TuiRouteCheck[] routeChecks)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(printDeviceCapabilities);
        ArgumentNullException.ThrowIfNull(routeChecks);

        Manifest = manifest;
        PrintDeviceCapabilities = printDeviceCapabilities;
        RouteChecks = routeChecks;
    }

    internal TuiAssetValidation Manifest { get; }

    internal IReadOnlyList<TuiAssetValidation> PrintDeviceCapabilities { get; }

    internal IReadOnlyList<TuiRouteCheck> RouteChecks { get; }

    internal static async Task<TuiDashboardModel> LoadAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        string appDirectory = ResolveAppDirectory(workingDirectory);
        string manifestPath = Path.Combine(appDirectory, "Package.appxmanifest");
        ManifestLintResult manifestResult = ManifestLinter.Lint(manifestPath);
        TuiAssetValidation manifest = new(
            "Manifest",
            manifestPath,
            manifestResult.Succeeded,
            manifestResult.Messages);

        TuiAssetValidation[] pdcValidations = ValidatePrintDeviceCapabilities(appDirectory);
        TuiRouteCheck[] routeChecks = await RunRouteChecksAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TuiDashboardModel(manifest, pdcValidations, routeChecks);
    }

    private static TuiAssetValidation[] ValidatePrintDeviceCapabilities(string appDirectory)
    {
        string configDirectory = Path.Combine(appDirectory, "Config");
        List<TuiAssetValidation> validations = [];
        foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
        {
            string assetName = GetConfigAssetName(endpoint.Kind);
            string pdcPath = Path.Combine(configDirectory, $"Printer{assetName}.pdc.xml");
            string pdrPath = Path.Combine(configDirectory, $"Printer{assetName}.pdr.xml");
            ValidationResult result = PdcValidator.Validate(pdcPath, pdrPath);
            validations.Add(new TuiAssetValidation(assetName, pdcPath, result.Succeeded, result.Messages));
        }

        return [.. validations];
    }

    private static async Task<TuiRouteCheck[]> RunRouteChecksAsync(CancellationToken cancellationToken)
    {
        List<TuiRouteCheck> checks = [];
        foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
        {
            checks.Add(await RunRouteCheckAsync(endpoint, cancellationToken).ConfigureAwait(false));
        }

        return [.. checks];
    }

    private static async Task<TuiRouteCheck> RunRouteCheckAsync(
        VirtualEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        CapturingSink cloudSink = new();
        EndpointSinkResolver sinkResolver = new(new Dictionary<EndpointKind, ISink>
        {
            [EndpointKind.Pdf] = new TargetStreamSink(),
            [EndpointKind.Xps] = new TargetStreamSink(),
            [EndpointKind.PostScript] = new TargetStreamSink(),
            [EndpointKind.PwgRaster] = new TargetStreamSink(),
            [EndpointKind.Pclm] = new TargetStreamSink(),
            [EndpointKind.Cloud] = cloudSink,
        });
        string contentType = PdlFormatInfo.GetContentType(endpoint.PreferredInputFormat);
        FixtureVirtualPrinterJob job = new(contentType, endpoint, null, null);
        VirtualPrinterJobProcessor processor = new(new PdlRouter(), new FixturePdlConverter(), sinkResolver);

        try
        {
            VirtualPrinterJobResult result = await processor.ProcessAsync(job, cancellationToken)
                .ConfigureAwait(false);
            long outputBytes = endpoint.Kind == EndpointKind.Cloud
                ? cloudSink.BytesWritten
                : job.OutputBytes;

            return new TuiRouteCheck(
                endpoint.QueueName,
                contentType,
                result.Plan.ActionKind,
                result.Plan.ConversionKind,
                result.Status,
                outputBytes);
        }
        finally
        {
            job.DeleteTemporaryOutput();
        }
    }

    private static string ResolveAppDirectory(string workingDirectory)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(workingDirectory));
        while (directory is not null)
        {
            string appDirectory = Path.Combine(directory.FullName, "src", "PrintSink.App");
            if (File.Exists(Path.Combine(appDirectory, "Package.appxmanifest")))
            {
                return appDirectory;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Path.GetFullPath(workingDirectory), "src", "PrintSink.App");
    }

    private static string GetConfigAssetName(EndpointKind kind)
    {
        return kind switch
        {
            EndpointKind.Pdf => "Pdf",
            EndpointKind.Xps => "Xps",
            EndpointKind.PostScript => "PostScript",
            EndpointKind.Cloud => "Cloud",
            EndpointKind.PwgRaster => "PwgRaster",
            EndpointKind.Pclm => "Pclm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown endpoint kind."),
        };
    }
}
