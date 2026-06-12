using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using Windows.ApplicationModel;
using Windows.Devices.Printers;

namespace PrintSink.App;

/// <summary>
/// Provisions the package's virtual printer queues through the Windows virtual-printer API.
/// </summary>
internal static class VirtualPrinterInstaller
{
    private const string EntryPoint = "PrintSink.Tasks.VirtualPrinterBackgroundTask";

    internal static async Task InstallAllAsync(CancellationToken cancellationToken)
    {
        foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VirtualPrinterInstallationParameters parameters = CreateParameters(endpoint);
            VirtualPrinterInstallationResult result = await VirtualPrinterManager
                .InstallVirtualPrinterAsync(parameters)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (result.Status is not VirtualPrinterInstallationStatus.InstallationSucceeded
                and not VirtualPrinterInstallationStatus.PrinterAlreadyInstalled)
            {
                string error = result.ExtendedError?.Message ?? "No extended error was reported.";
                throw new InvalidOperationException(
                    $"Failed to install virtual printer '{endpoint.QueueName}': {result.Status}. {error}");
            }
        }
    }

    internal static async Task RemoveAllAsync(CancellationToken cancellationToken)
    {
        string packageFamilyName = Package.Current.Id.FamilyName;
        HashSet<string> installedPrinters = VirtualPrinterManager
            .FindAllVirtualPrinters(packageFamilyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (VirtualEndpoint endpoint in EndpointCatalog.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!installedPrinters.Contains(endpoint.QueueName))
            {
                continue;
            }

            await VirtualPrinterManager
                .RemoveVirtualPrinterAsync(endpoint.QueueName)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static VirtualPrinterInstallationParameters CreateParameters(VirtualEndpoint endpoint)
    {
        VirtualPrinterInstallationParameters parameters = new()
        {
            PrinterName = endpoint.QueueName,
            PrinterUri = endpoint.PrinterUri,
            PrintDeviceCapabilitiesPackageRelativeFilePath = GetPdcPath(endpoint.Kind),
            PrintDeviceResourcesPackageRelativeFilePath = GetPdrPath(endpoint.Kind),
            PreferredInputFormat = GetPreferredInputFormat(endpoint.PreferredInputFormat),
            EntryPoint = EntryPoint,
        };

        if (endpoint.RequiresTargetFile)
        {
            string extension = endpoint.DefaultExtension?.TrimStart('.') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new InvalidOperationException($"Endpoint '{endpoint.QueueName}' requires an output extension.");
            }

            parameters.OutputFileExtensions.Add(extension);
        }

        foreach (PdlFormat passthroughFormat in endpoint.PassthroughFormats)
        {
            parameters.SupportedInputFormats.Add(CreateSupportedInputFormat(passthroughFormat));
        }

        return parameters;
    }

    private static VirtualPrinterSupportedFormat CreateSupportedInputFormat(PdlFormat format)
    {
        return new VirtualPrinterSupportedFormat(PdlFormatInfo.GetContentType(format), GetMaxSupportedVersion(format));
    }

    private static VirtualPrinterPreferredInputFormat GetPreferredInputFormat(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Oxps => VirtualPrinterPreferredInputFormat.OpenXps,
            PdlFormat.PostScript => VirtualPrinterPreferredInputFormat.PostScript,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported preferred input format."),
        };
    }

    private static string GetMaxSupportedVersion(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Pdf => "1.7",
            PdlFormat.PostScript => "3.0",
            PdlFormat.Oxps or PdlFormat.Xps => "1.0",
            PdlFormat.PwgRaster or PdlFormat.Pclm => "1.0",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported passthrough format."),
        };
    }

    private static string GetPdcPath(EndpointKind kind)
    {
        return $"Config\\{GetConfigurationStem(kind)}.pdc.xml";
    }

    private static string GetPdrPath(EndpointKind kind)
    {
        return $"Config\\{GetConfigurationStem(kind)}.pdr.xml";
    }

    private static string GetConfigurationStem(EndpointKind kind)
    {
        return kind switch
        {
            EndpointKind.Pdf => "PrinterPdf",
            EndpointKind.Xps => "PrinterXps",
            EndpointKind.PostScript => "PrinterPostScript",
            EndpointKind.Cloud => "PrinterCloud",
            EndpointKind.PwgRaster => "PrinterPwgRaster",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported endpoint kind."),
        };
    }
}
