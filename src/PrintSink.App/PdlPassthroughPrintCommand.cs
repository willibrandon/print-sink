using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using AttributeMergePolicyOptions = PrintSink.Core.Tickets.AttributeMergePolicyOptions;
using CoreIppAttributeValue = PrintSink.Core.Tickets.IppAttributeValue;
using IppAttributeMapper = PrintSink.Core.Tickets.IppAttributeMapper;
using System.Globalization;
using System.Xml.Linq;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.PrintTicket;
using Windows.Storage.Streams;
using WinRT;

namespace PrintSink.App;

/// <summary>
/// Sends source PDL through the Windows PDL passthrough provider.
/// </summary>
internal static class PdlPassthroughPrintCommand
{
    private const int BufferSize = 81920;
    private static readonly TimeSpan CapabilityRefreshTimeout = TimeSpan.FromSeconds(30);

    internal static async Task<(int PrintJobId, string ProviderDetail)> PrintPdfAsync(
        EndpointKind endpointKind,
        string sourcePath,
        Func<int, string, CancellationToken, Task>? printTargetCreated,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        if (!endpoint.SupportsPassthrough(PdlFormat.Pdf))
        {
            throw new ArgumentException(
                $"Endpoint '{endpoint.QueueName}' does not support PDF passthrough.",
                nameof(endpointKind));
        }

        return await PrintPdfToPrinterAsync(
                endpoint.QueueName,
                sourcePath,
                printTargetCreated,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<(int PrintJobId, string ProviderDetail)> PrintPdfToPrinterAsync(
        string printerName,
        string sourcePath,
        Func<int, string, CancellationToken, Task>? printTargetCreated,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("PDF passthrough source was not found.", fullSourcePath);
        }

        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(printerName);
        bool pdlPassthroughSupported = printDevice.IsPdlPassthroughSupported(PdlFormatInfo.PdfContentType);
        if (!pdlPassthroughSupported)
        {
            TryRefreshPrintDeviceCapabilities(printerName);
            printDevice = IppPrintDevice.FromPrinterName(printerName);
            pdlPassthroughSupported = printDevice.IsPdlPassthroughSupported(PdlFormatInfo.PdfContentType);
        }

        string jobName = Path.GetFileNameWithoutExtension(fullSourcePath);
        PdlPassthroughProvider provider = printDevice.GetPdlPassthroughProvider();
        bool useProvider2 = UniversalApiContract19PrinterApis.TryCreateSupportedPdlPassthroughProvider2Reference(
            provider,
            out IObjectReference? provider2Reference,
            out string providerDetail);
        providerDetail = $"{providerDetail}; pdlSupportProbe={pdlPassthroughSupported}";
        PdlPassthroughTarget? target = null;
        InMemoryRandomAccessStream? printTicketStream = null;
        IInputStream? printTicketInput = null;
        try
        {
            if (useProvider2 && provider2Reference is not null)
            {
                try
                {
                    (IBuffer jobAttributes, IBuffer operationAttributes, string attributeDetail) =
                        CreateIppAttributeBuffers(
                            printDevice,
                            printDevice.UserDefaultPrintTicket,
                            jobName,
                            PdlFormatInfo.PdfContentType);
                    target = UniversalApiContract19PrinterApis.StartPrintJobWithIppJobAttributes(
                        provider2Reference,
                        jobName,
                        PdlFormatInfo.PdfContentType,
                        jobAttributes,
                        operationAttributes);
                    providerDetail = $"{providerDetail}; provider2Submit=used; {attributeDetail}";
                }
                catch (Exception ex) when (CanFallbackFromProvider2(ex))
                {
                    providerDetail = CreateProvider2FallbackDetail(
                        providerDetail,
                        "ipp-attribute-conversion-failed",
                        ex);
                }
            }

            if (target is null)
            {
                printTicketStream = await CreatePrintTicketStreamAsync(
                    printDevice.UserDefaultPrintTicket,
                    cancellationToken)
                    .ConfigureAwait(false);
                printTicketInput = printTicketStream.GetInputStreamAt(0);
                PageConfigurationSettings pageConfiguration = new()
                {
                    OrientationSource = PageConfigurationSource.PdlContent,
                    SizeSource = PageConfigurationSource.PdlContent,
                };

                target = provider.StartPrintJobWithPrintTicket(
                    jobName,
                    PdlFormatInfo.PdfContentType,
                    printTicketInput,
                    pageConfiguration);
            }

            PdlPassthroughTarget activeTarget = target
                ?? throw new InvalidOperationException("The PDF passthrough provider did not create a print target.");
            int printJobId = activeTarget.PrintJobId;
            if (printTargetCreated is not null)
            {
                await printTargetCreated(printJobId, providerDetail, cancellationToken).ConfigureAwait(false);
            }

            using IOutputStream output = activeTarget.GetOutputStream();
            await WriteFileToOutputAsync(fullSourcePath, output, cancellationToken).ConfigureAwait(false);

            activeTarget.Submit();
            return (printJobId, providerDetail);
        }
        finally
        {
            DisposeTarget(target);
            provider2Reference?.Dispose();
            printTicketInput?.Dispose();
            printTicketStream?.Dispose();
        }
    }

    private static bool CanFallbackFromProvider2(Exception exception)
    {
        return exception is InvalidOperationException or System.Runtime.InteropServices.COMException;
    }

    private static string CreateProvider2FallbackDetail(
        string providerDetail,
        string fallbackReason,
        Exception exception)
    {
        string normalizedProviderDetail = providerDetail.Replace(
            "provider2=supported",
            "provider2=runtime-unusable; provider2Probe=supported",
            StringComparison.Ordinal);

        return string.Join(
            "; ",
            normalizedProviderDetail,
            "provider2Submit=fallback-v1",
            $"provider2Fallback={fallbackReason}",
            $"provider2FallbackHResult=0x{exception.HResult:X8}",
            $"provider2FallbackException={exception.GetType().Name}",
            $"provider2FallbackMessage={SanitizeProviderDetail(exception.Message)}");
    }

    private static (IBuffer JobAttributes, IBuffer OperationAttributes, string Detail) CreateIppAttributeBuffers(
        IppPrintDevice printDevice,
        WorkflowPrintTicket printTicket,
        string jobName,
        string targetPdlFormat)
    {
        try
        {
            return CreateIppAttributeBuffersFromPrintTicket(printDevice, printTicket, targetPdlFormat);
        }
        catch (Exception ex) when (CanFallbackFromProvider2(ex))
        {
            return CreateCoreMappedIppAttributeBuffers(printTicket, jobName, targetPdlFormat, ex);
        }
    }

    private static (IBuffer JobAttributes, IBuffer OperationAttributes, string Detail) CreateIppAttributeBuffersFromPrintTicket(
        IppPrintDevice printDevice,
        WorkflowPrintTicket printTicket,
        string targetPdlFormat)
    {
        var attributesByGroup = IppAttributeConverter.ConvertPrintTicketToIppAttributesForPrinter(
            printDevice.PrinterName,
            printTicket,
            targetPdlFormat);

        IDictionary<string, IppAttributeValue> jobAttributes = attributesByGroup.TryGetValue(
            IppAttributeGroupKind.Job,
            out IDictionary<string, IppAttributeValue>? resolvedJobAttributes)
            ? resolvedJobAttributes
            : new Dictionary<string, IppAttributeValue>(StringComparer.OrdinalIgnoreCase);
        IDictionary<string, IppAttributeValue> operationAttributes = attributesByGroup.TryGetValue(
            IppAttributeGroupKind.Operation,
            out IDictionary<string, IppAttributeValue>? resolvedOperationAttributes)
            ? resolvedOperationAttributes
            : new Dictionary<string, IppAttributeValue>(StringComparer.OrdinalIgnoreCase);

        IBuffer jobAttributesBuffer = IppAttributeConverter.ConvertIppAttributesToBuffer(
            jobAttributes,
            IppAttributeGroupKind.Job);
        IBuffer operationAttributesBuffer = IppAttributeConverter.ConvertIppAttributesToBuffer(
            operationAttributes,
            IppAttributeGroupKind.Operation);

        string detail = string.Join(
            "; ",
            "ippAttributeSource=print-ticket-converter",
            $"ippJobAttributes={jobAttributes.Count}",
            $"ippJobAttributeBytes={jobAttributesBuffer.Length}",
            $"ippOperationAttributes={operationAttributes.Count}",
            $"ippOperationAttributeBytes={operationAttributesBuffer.Length}");
        return (jobAttributesBuffer, operationAttributesBuffer, detail);
    }

    private static (IBuffer JobAttributes, IBuffer OperationAttributes, string Detail) CreateCoreMappedIppAttributeBuffers(
        WorkflowPrintTicket printTicket,
        string jobName,
        string targetPdlFormat,
        Exception converterException)
    {
        IppAttributeMapper mapper = new();
        XDocument printTicketXml = XDocument.Parse(printTicket.XmlNode.GetXml(), LoadOptions.None);
        IReadOnlyDictionary<string, CoreIppAttributeValue> mappedAttributes = mapper.ApplyMergePolicy(
            mapper.FromPrintTicket(printTicketXml),
            AttributeMergePolicyOptions.RemovePdlEmbeddedMediaSize);
        Dictionary<string, IppAttributeValue> jobAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["job-name"] = IppAttributeValue.CreateNameWithoutLanguage(jobName),
        };

        foreach (KeyValuePair<string, CoreIppAttributeValue> mappedAttribute in mappedAttributes)
        {
            if (TryCreateWinRtJobAttribute(mappedAttribute.Value, out IppAttributeValue? winRtAttribute)
                && winRtAttribute is not null)
            {
                jobAttributes[mappedAttribute.Key] = winRtAttribute;
            }
        }

        if (!jobAttributes.ContainsKey("copies") && UserDefaultPrintTicketEditor.ReadCopies(printTicket) is int copyCount)
        {
            jobAttributes["copies"] = IppAttributeValue.CreateInteger(copyCount);
        }

        Dictionary<string, IppAttributeValue> operationAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["attributes-charset"] = IppAttributeValue.CreateCharset("utf-8"),
            ["attributes-natural-language"] = IppAttributeValue.CreateNaturalLanguage(GetIppNaturalLanguage()),
            ["document-format"] = IppAttributeValue.CreateMimeMedia(targetPdlFormat),
            ["requesting-user-name"] = IppAttributeValue.CreateNameWithoutLanguage(Environment.UserName),
        };

        IBuffer jobAttributesBuffer = IppAttributeConverter.ConvertIppAttributesToBuffer(
            jobAttributes,
            IppAttributeGroupKind.Job);
        IBuffer operationAttributesBuffer = IppAttributeConverter.ConvertIppAttributesToBuffer(
            operationAttributes,
            IppAttributeGroupKind.Operation);

        string detail = string.Join(
            "; ",
            "ippAttributeSource=core-fallback",
            $"ippAttributeFallbackHResult=0x{converterException.HResult:X8}",
            $"ippAttributeFallbackException={converterException.GetType().Name}",
            $"ippMappedJobAttributes={mappedAttributes.Count}",
            $"ippMappedJobAttributeNames={FormatAttributeNames(mappedAttributes.Keys)}",
            $"ippJobAttributes={jobAttributes.Count}",
            $"ippJobAttributeBytes={jobAttributesBuffer.Length}",
            $"ippOperationAttributes={operationAttributes.Count}",
            $"ippOperationAttributeBytes={operationAttributesBuffer.Length}");
        return (jobAttributesBuffer, operationAttributesBuffer, detail);
    }

    private static bool TryCreateWinRtJobAttribute(
        CoreIppAttributeValue attribute,
        out IppAttributeValue? winRtAttribute)
    {
        winRtAttribute = null;
        if (attribute.Collections.Count > 0 || attribute.Values.Count == 0)
        {
            return false;
        }

        string[] values = [.. attribute.Values];
        winRtAttribute = attribute.Name switch
        {
            "copies" or "number-up" => CreateIntegerAttribute(values),
            "finishings" or "orientation-requested" or "print-quality" => CreateEnumAttribute(values),
            _ => CreateKeywordAttribute(values),
        };
        return winRtAttribute is not null;
    }

    private static IppAttributeValue? CreateIntegerAttribute(string[] values)
    {
        int[] parsedValues = ParseIntegerValues(values);
        return parsedValues.Length switch
        {
            0 => null,
            1 => IppAttributeValue.CreateInteger(parsedValues[0]),
            _ => IppAttributeValue.CreateIntegerArray(parsedValues),
        };
    }

    private static IppAttributeValue? CreateEnumAttribute(string[] values)
    {
        int[] parsedValues = ParseIntegerValues(values);
        return parsedValues.Length switch
        {
            0 => null,
            1 => IppAttributeValue.CreateEnum(parsedValues[0]),
            _ => IppAttributeValue.CreateEnumArray(parsedValues),
        };
    }

    private static IppAttributeValue CreateKeywordAttribute(string[] values)
    {
        return values.Length == 1
            ? IppAttributeValue.CreateKeyword(values[0])
            : IppAttributeValue.CreateKeywordArray(values);
    }

    private static int[] ParseIntegerValues(string[] values)
    {
        List<int> parsedValues = [];
        foreach (string value in values)
        {
            if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedValue))
            {
                return [];
            }

            parsedValues.Add(parsedValue);
        }

        return [.. parsedValues];
    }

    private static string FormatAttributeNames(IEnumerable<string> attributeNames)
    {
        string[] names = [.. attributeNames.Order(StringComparer.OrdinalIgnoreCase)];
        return names.Length == 0 ? "<none>" : string.Join(',', names);
    }

    private static string GetIppNaturalLanguage()
    {
        string language = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrWhiteSpace(language)
            ? "en-US"
            : language;
    }

    private static string SanitizeProviderDetail(string detail)
    {
        return detail
            .ReplaceLineEndings(" ")
            .Replace(';', ',')
            .Trim();
    }

    private static void DisposeTarget(PdlPassthroughTarget? target)
    {
        target?.Dispose();
    }

    private static void TryRefreshPrintDeviceCapabilities(string printerName)
    {
        try
        {
            PrintDeviceCapabilityRefresher.Refresh(printerName, CapabilityRefreshTimeout);
        }
        catch (Exception ex) when (ex is TimeoutException || CanFallbackFromProvider2(ex))
        {
        }
    }

    private static async Task<InMemoryRandomAccessStream> CreatePrintTicketStreamAsync(
        WorkflowPrintTicket printTicket,
        CancellationToken cancellationToken)
    {
        string printTicketXml = printTicket.XmlNode.GetXml();
        InMemoryRandomAccessStream stream = new();
        using IOutputStream output = stream.GetOutputStreamAt(0);
        using DataWriter writer = new(output)
        {
            UnicodeEncoding = UnicodeEncoding.Utf8,
        };

        writer.WriteString(printTicketXml);
        await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        writer.DetachStream();
        stream.Seek(0);
        return stream;
    }

    private static async Task WriteFileToOutputAsync(
        string sourcePath,
        IOutputStream output,
        CancellationToken cancellationToken)
    {
        FileStream source = File.OpenRead(sourcePath);
        using DataWriter writer = new(output);
        await using (source.ConfigureAwait(false))
        {
            byte[] buffer = new byte[BufferSize];
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (read == buffer.Length)
                {
                    writer.WriteBytes(buffer);
                }
                else
                {
                    byte[] slice = new byte[read];
                    System.Buffer.BlockCopy(buffer, 0, slice, 0, read);
                    writer.WriteBytes(slice);
                }

                await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
            }

            await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
            writer.DetachStream();
        }
    }
}
