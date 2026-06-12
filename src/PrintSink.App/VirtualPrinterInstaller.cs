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
        string? blockerMessage = GetProvisioningBlockerMessage();
        if (blockerMessage is not null)
        {
            throw new InvalidOperationException(blockerMessage);
        }

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

    internal static string? GetProvisioningBlockerMessage()
    {
        return GetProvisioningBlockerMessage(Package.Current.InstalledLocation.Path);
    }

    internal static string? GetProvisioningBlockerMessage(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);

        if (IsLooseDevelopmentLayout(packageRoot))
        {
            return $"Virtual printer provisioning requires an installed MSIX package. The current PrintSink package is a loose development layout at '{packageRoot}'; install a signed MSIX, then run 'dotnet run --project src\\PrintSink.Cli -- queues install'.";
        }

        return null;
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
            if (endpoint.OutputExtensions.Count == 0)
            {
                throw new InvalidOperationException($"Endpoint '{endpoint.QueueName}' requires at least one output extension.");
            }

            foreach (string extension in endpoint.OutputExtensions)
            {
                parameters.OutputFileExtensions.Add(extension.TrimStart('.'));
            }
        }

        foreach (PdlFormat passthroughFormat in endpoint.PassthroughFormats)
        {
            parameters.SupportedInputFormats.Add(CreateSupportedInputFormat(passthroughFormat));
        }

        return parameters;
    }

    private static VirtualPrinterSupportedFormat CreateSupportedInputFormat(PdlFormat format)
    {
        return new VirtualPrinterSupportedFormat(
            PdlFormatInfo.GetContentType(format),
            PdlFormatInfo.GetMaxSupportedVersion(format));
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
            EndpointKind.Pclm => "PrinterPclm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported endpoint kind."),
        };
    }

    private static bool IsLooseDevelopmentLayout(string packageRoot)
    {
        string normalized = packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (File.Exists(Path.Combine(normalized, "AppxSignature.p7x")))
        {
            return false;
        }

        return normalized.EndsWith(
                string.Concat(Path.DirectorySeparatorChar, "AppX"),
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                string.Concat(Path.DirectorySeparatorChar, "bin", Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }
}
