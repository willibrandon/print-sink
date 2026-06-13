using SharpIpp;
using SharpIpp.Exceptions;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace PrintSink.E2E.IppPrinter;

internal sealed class IppPrinterServer
{
    private readonly Lock gate = new();
    private readonly SharpIppServer sharpIppServer = new();
    private readonly IppPrinterOptions options;
    private readonly IppPrinterEvidenceWriter evidenceWriter;
    private readonly List<IppPrinterJob> jobs = [];
    private readonly List<IppRequestRecord> requests = [];
    private int nextJobId = 1000;

    internal IppPrinterServer(IppPrinterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        evidenceWriter = new IppPrinterEvidenceWriter(options.OutputDirectory);
    }

    internal async Task ProcessAsync(
        Stream requestStream,
        Stream responseStream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(responseStream);

        try
        {
            IIppRequestMessage rawRequest = await sharpIppServer
                .ReceiveRawRequestAsync(requestStream, cancellationToken)
                .ConfigureAwait(false);
            IIppRequest request = await sharpIppServer
                .ReceiveRequestAsync(rawRequest, cancellationToken)
                .ConfigureAwait(false);

            IIppResponse response = await CreateResponseAsync(rawRequest, request, cancellationToken)
                .ConfigureAwait(false);
            IIppResponseMessage rawResponse = await sharpIppServer
                .CreateRawResponseAsync(response, cancellationToken)
                .ConfigureAwait(false);
            ImproveRawResponse(request, rawResponse);
            RecordRequest(rawRequest, rawResponse);

            await sharpIppServer
                .SendRawResponseAsync(rawResponse, responseStream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IppRequestException ex)
        {
            await SendErrorResponseAsync(ex, responseStream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await File.AppendAllTextAsync(
                    options.ErrorLogPath,
                    DateTimeOffset.UtcNow.ToString("O") + Environment.NewLine + ex + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    private Task<IIppResponse> CreateResponseAsync(
        IIppRequestMessage rawRequest,
        IIppRequest request,
        CancellationToken cancellationToken)
    {
        return request switch
        {
            GetPrinterAttributesRequest getPrinterAttributes => Task.FromResult<IIppResponse>(
                CreateGetPrinterAttributesResponse(getPrinterAttributes)),
            ValidateJobRequest validateJob => Task.FromResult<IIppResponse>(
                CreateValidateJobResponse(validateJob)),
            CreateJobRequest createJob => Task.FromResult<IIppResponse>(
                CreateCreateJobResponse(rawRequest, createJob)),
            PrintJobRequest printJob => CreatePrintJobResponseAsync(rawRequest, printJob, cancellationToken),
            SendDocumentRequest sendDocument => CreateSendDocumentResponseAsync(rawRequest, sendDocument, cancellationToken),
            GetJobAttributesRequest getJobAttributes => Task.FromResult<IIppResponse>(
                CreateGetJobAttributesResponse(getJobAttributes)),
            GetJobsRequest getJobs => Task.FromResult<IIppResponse>(
                CreateGetJobsResponse(getJobs)),
            CancelJobRequest cancelJob => Task.FromResult<IIppResponse>(
                CreateCancelJobResponse(cancelJob)),
            _ => Task.FromResult<IIppResponse>(CreateUnsupportedResponse(request)),
        };
    }

    private GetPrinterAttributesResponse CreateGetPrinterAttributesResponse(GetPrinterAttributesRequest request)
    {
        return new GetPrinterAttributesResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.SuccessfulOk,
            PrinterAttributes = new PrinterDescriptionAttributes
            {
                CharsetConfigured = "utf-8",
                CharsetSupported = ["utf-8"],
                CompressionSupported = [Compression.None],
                DocumentFormatDefault = options.DocumentFormat,
                DocumentFormatSupported =
                [
                    options.DocumentFormat,
                    "application/pdf",
                    "image/pwg-raster",
                    "application/PCLm",
                ],
                GeneratedNaturalLanguageSupported = ["en-us"],
                IppVersionsSupported = [new IppVersion(1, 1), new IppVersion(2, 0)],
                MultipleDocumentJobsSupported = true,
                NaturalLanguageConfigured = "en-us",
                OperationsSupported =
                [
                    IppOperation.PrintJob,
                    IppOperation.ValidateJob,
                    IppOperation.CreateJob,
                    IppOperation.SendDocument,
                    IppOperation.CancelJob,
                    IppOperation.GetJobAttributes,
                    IppOperation.GetJobs,
                    IppOperation.GetPrinterAttributes,
                ],
                PrinterInfo = options.PrinterName,
                PrinterIsAcceptingJobs = !options.RejectJobs,
                PrinterMakeAndModel = "PrintSink E2E IPP Printer",
                PrinterName = options.PrinterName,
                PrinterState = GetPrinterState(),
                PrinterStateReasons = options.PrinterStateReasons,
                PrinterUriSupported = [options.PrinterUri],
                QueuedJobCount = GetQueuedJobCount(),
                UriAuthenticationSupported = [UriAuthentication.None],
                UriSecuritySupported = [UriSecurity.None],
            },
        };
    }

    private ValidateJobResponse CreateValidateJobResponse(ValidateJobRequest request)
    {
        return new ValidateJobResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = options.RejectJobs
                ? IppStatusCode.ServerErrorNotAcceptingJobs
                : IppStatusCode.SuccessfulOk,
        };
    }

    private CreateJobResponse CreateCreateJobResponse(
        IIppRequestMessage rawRequest,
        CreateJobRequest request)
    {
        if (options.RejectJobs)
        {
            return new CreateJobResponse
            {
                RequestId = request.RequestId,
                Version = request.Version,
                StatusCode = IppStatusCode.ServerErrorNotAcceptingJobs,
                JobAttributes = new JobAttributes(),
            };
        }

        IppPrinterJob job = CreateJob(
            request.OperationAttributes?.JobName,
            AttributeNames(rawRequest.OperationAttributes),
            AttributeNames(rawRequest.JobAttributes));

        return new CreateJobResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.SuccessfulOk,
            JobAttributes = CreateResponseJobAttributes(job),
        };
    }

    private async Task<IIppResponse> CreatePrintJobResponseAsync(
        IIppRequestMessage rawRequest,
        PrintJobRequest request,
        CancellationToken cancellationToken)
    {
        if (options.RejectJobs)
        {
            return new PrintJobResponse
            {
                RequestId = request.RequestId,
                Version = request.Version,
                StatusCode = IppStatusCode.ServerErrorNotAcceptingJobs,
                JobAttributes = new JobAttributes(),
            };
        }

        IppPrinterJob job = CreateJob(
            request.OperationAttributes?.JobName,
            AttributeNames(rawRequest.OperationAttributes),
            AttributeNames(rawRequest.JobAttributes));
        await SaveDocumentAsync(
                job,
                request.Document,
                request.OperationAttributes?.DocumentFormat?.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        return new PrintJobResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.SuccessfulOk,
            JobAttributes = CreateResponseJobAttributes(job),
        };
    }

    private async Task<IIppResponse> CreateSendDocumentResponseAsync(
        IIppRequestMessage rawRequest,
        SendDocumentRequest request,
        CancellationToken cancellationToken)
    {
        IppPrinterJob? job = FindJob(request.OperationAttributes?.JobId);
        if (job is null || options.RejectJobs)
        {
            return new SendDocumentResponse
            {
                RequestId = request.RequestId,
                Version = request.Version,
                StatusCode = options.RejectJobs
                    ? IppStatusCode.ServerErrorNotAcceptingJobs
                    : IppStatusCode.ClientErrorNotPossible,
                JobAttributes = new JobAttributes(),
            };
        }

        AddDistinct(job.OperationAttributes, AttributeNames(rawRequest.OperationAttributes));
        AddDistinct(job.JobAttributes, AttributeNames(rawRequest.JobAttributes));
        await SaveDocumentAsync(
                job,
                request.Document,
                request.OperationAttributes?.DocumentFormat?.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        return new SendDocumentResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.SuccessfulOk,
            JobAttributes = CreateResponseJobAttributes(job),
        };
    }

    private GetJobAttributesResponse CreateGetJobAttributesResponse(GetJobAttributesRequest request)
    {
        IppPrinterJob? job = FindJob(request.OperationAttributes?.JobId);
        return new GetJobAttributesResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = job is null ? IppStatusCode.ClientErrorNotPossible : IppStatusCode.SuccessfulOk,
            JobAttributes = job is null ? new JobDescriptionAttributes() : CreateJobDescriptionAttributes(job),
        };
    }

    private GetJobsResponse CreateGetJobsResponse(GetJobsRequest request)
    {
        IppPrinterJob[] snapshot;
        lock (gate)
        {
            snapshot = [.. jobs];
        }

        return new GetJobsResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.SuccessfulOk,
            JobsAttributes = [.. snapshot.Select(CreateJobDescriptionAttributes)],
        };
    }

    private CancelJobResponse CreateCancelJobResponse(CancelJobRequest request)
    {
        IppPrinterJob? job = FindJob(request.OperationAttributes?.JobId);
        if (job is not null)
        {
            lock (gate)
            {
                job.State = "canceled";
                job.CompletedUtc = DateTimeOffset.UtcNow;
                WriteEvidenceUnsafe();
            }
        }

        return new CancelJobResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = job is null ? IppStatusCode.ClientErrorNotPossible : IppStatusCode.SuccessfulOk,
        };
    }

    private static ValidateJobResponse CreateUnsupportedResponse(IIppRequest request)
    {
        return new ValidateJobResponse
        {
            RequestId = request.RequestId,
            Version = request.Version,
            StatusCode = IppStatusCode.ServerErrorOperationNotSupported,
        };
    }

    private IppPrinterJob CreateJob(
        string? jobName,
        IReadOnlyList<string> operationAttributes,
        IReadOnlyList<string> jobAttributes)
    {
        lock (gate)
        {
            IppPrinterJob job = new(
                Interlocked.Increment(ref nextJobId),
                jobName,
                DateTimeOffset.UtcNow,
                operationAttributes,
                jobAttributes);
            jobs.Add(job);
            WriteEvidenceUnsafe();
            return job;
        }
    }

    private async Task SaveDocumentAsync(
        IppPrinterJob job,
        Stream? document,
        string? documentFormat,
        CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return;
        }

        string normalizedFormat = string.IsNullOrWhiteSpace(documentFormat)
            ? options.DocumentFormat
            : documentFormat;
        string extension = GetExtension(normalizedFormat);
        string documentPath = Path.Combine(options.OutputDirectory, $"job-{job.Id}{extension}");
        FileStream output = File.Create(documentPath);
        await using (output.ConfigureAwait(false))
        {
            if (document.CanSeek)
            {
                document.Position = 0;
            }

            await document.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        FileInfo outputFile = new(documentPath);
        lock (gate)
        {
            job.DocumentFormat = normalizedFormat;
            job.DocumentPath = documentPath;
            job.DocumentBytes = outputFile.Length;
            job.State = "completed";
            job.CompletedUtc = DateTimeOffset.UtcNow;
            WriteEvidenceUnsafe();
        }
    }

    private IppPrinterJob? FindJob(int? jobId)
    {
        if (jobId is null)
        {
            return null;
        }

        lock (gate)
        {
            return jobs.FirstOrDefault(job => job.Id == jobId.Value);
        }
    }

    private static JobAttributes CreateResponseJobAttributes(IppPrinterJob job)
    {
        return new JobAttributes
        {
            JobId = job.Id,
            JobState = ParseJobState(job.State),
            JobStateReasons = [JobStateReason.None],
        };
    }

    private JobDescriptionAttributes CreateJobDescriptionAttributes(IppPrinterJob job)
    {
        return new JobDescriptionAttributes
        {
            JobId = job.Id,
            JobName = job.Name,
            JobPrinterUri = options.PrinterUri,
            JobState = ParseJobState(job.State),
            JobStateReasons = [JobStateReason.None],
            DateTimeAtCreation = job.CreatedUtc,
            DateTimeAtCompleted = job.CompletedUtc ?? DateTimeOffset.MinValue,
        };
    }

    private static JobState ParseJobState(string state)
    {
        return state switch
        {
            "completed" => JobState.Completed,
            "canceled" => JobState.Canceled,
            _ => JobState.Pending,
        };
    }

    private PrinterState GetPrinterState()
    {
        if (options.PrinterState != PrinterState.Idle)
        {
            return options.PrinterState;
        }

        return GetQueuedJobCount() > 0 ? PrinterState.Processing : PrinterState.Idle;
    }

    private static int GetPrinterStateValue(PrinterState state)
    {
        return state switch
        {
            PrinterState.Idle => 3,
            PrinterState.Processing => 4,
            PrinterState.Stopped => 5,
            _ => (int)state,
        };
    }

    private int GetQueuedJobCount()
    {
        lock (gate)
        {
            return jobs.Count(job => job.State == "pending");
        }
    }

    private void RecordRequest(IIppRequestMessage rawRequest, IIppResponseMessage rawResponse)
    {
        IppAttribute[] responsePrinterAttributes = [.. rawResponse.PrinterAttributes.SelectMany(static group => group)];
        lock (gate)
        {
            requests.Add(
                new IppRequestRecord(
                    DateTimeOffset.UtcNow,
                    rawRequest.IppOperation.ToString(),
                    AttributeNames(rawRequest.OperationAttributes),
                    AttributeValues(rawRequest.OperationAttributes),
                    AttributeNames(rawRequest.JobAttributes),
                    AttributeNames(responsePrinterAttributes),
                    AttributeValues(responsePrinterAttributes)));
            WriteEvidenceUnsafe();
        }
    }

    private void WriteEvidenceUnsafe()
    {
        evidenceWriter.Write(options, jobs, requests);
    }

    private static IReadOnlyList<string> AttributeNames(IEnumerable<IppAttribute> attributes)
    {
        return [.. attributes
            .Select(static attribute => attribute.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static Dictionary<string, IReadOnlyList<string>> AttributeValues(IEnumerable<IppAttribute> attributes)
    {
        Dictionary<string, List<string>> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (IppAttribute attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Name))
            {
                continue;
            }

            if (!values.TryGetValue(attribute.Name, out List<string>? attributeValues))
            {
                attributeValues = [];
                values[attribute.Name] = attributeValues;
            }

            attributeValues.Add(FormatAttributeValue(attribute.Value));
        }

        return values.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)[.. pair.Value],
            StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatAttributeValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            byte[] bytes => Convert.ToBase64String(bytes),
            Array array => string.Join(",", array.Cast<object>().Select(FormatAttributeValue)),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(value);
            }
        }
    }

    private void ImproveRawResponse(IIppRequest request, IIppResponseMessage rawResponse)
    {
        if (request is not GetPrinterAttributesRequest)
        {
            return;
        }

        List<IppAttribute>? attributes = rawResponse.PrinterAttributes.FirstOrDefault();
        if (attributes is null)
        {
            return;
        }

        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.TextWithoutLanguage, "printer-device-id", GetPrinterDeviceId()));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Uri, "printer-uuid", "urn:uuid:6e660901-13c9-4436-a4e4-00000000e2e0"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.NameWithoutLanguage, "printer-dns-sd-name", options.PrinterName));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.TextWithoutLanguage, "printer-make-and-model", "PrintSink E2E IPP Printer"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Enum, "printer-state", GetPrinterStateValue(GetPrinterState())));
        AddMissingKeywordAttributes(
            attributes,
            "printer-state-reasons",
            [.. options.PrinterStateReasons.Select(static reason => reason.Value)]);
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Boolean, "printer-is-accepting-jobs", !options.RejectJobs));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.MimeMediaType, "document-format-preferred", options.DocumentFormat));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Integer, "job-password-supported", 255));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "media-default", "na_letter_8.5x11in"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "media-source-default", "auto"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "media-type-default", "stationery"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Integer, "copies-default", 1));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Integer, "number-up-default", 1));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Enum, "orientation-requested-default", 3));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "output-bin-default", "face-down"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "print-color-mode-default", "monochrome"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Enum, "print-quality-default", 4));
        AddMissingAttribute(
            attributes,
            new IppAttribute(Tag.Keyword, "sides-default", "one-sided"));
        AddMissingAttribute(
            attributes,
            new IppAttribute(
                Tag.Resolution,
                "printer-resolution-default",
                new Resolution(300, 300, ResolutionUnit.DotsPerInch, true)));

        AddMissingKeywordAttributes(attributes, "job-password-encryption-supported", "sha2-256", "none");
        AddMissingKeywordAttributes(attributes, "media-supported", "na_letter_8.5x11in", "northamericaletter");
        AddMissingKeywordAttributes(attributes, "media-source-supported", "auto", "automaticinputbin", "main", "tray-1");
        AddMissingKeywordAttributes(attributes, "media-type-supported", "stationery", "auto");
        AddMissingKeywordAttributes(attributes, "media-col-supported", "media-size", "media-type", "media-source");
        AddMissingKeywordAttributes(
            attributes,
            "multiple-document-handling-supported",
            "separate-documents-uncollated-copies",
            "separate-documents-collated-copies");
        AddMissingKeywordAttributes(attributes, "output-bin-supported", "face-down", "auto", "automationoutputbin");
        AddMissingKeywordAttributes(attributes, "page-delivery-supported", "same-order", "reverse-order", "oddpagesthenevenpages");
        AddMissingKeywordAttributes(attributes, "print-color-mode-supported", "monochrome", "color");
        AddMissingKeywordAttributes(attributes, "print-scaling-supported", "auto", "auto-fit", "fill", "fit", "none");
        AddMissingKeywordAttributes(attributes, "sides-supported", "one-sided", "two-sided-long-edge", "two-sided-short-edge");
        AddMissingIntegerAttributes(attributes, Tag.Enum, "finishings-supported", 3, 4, 20, 21, 22, 23);
        AddMissingIntegerAttributes(attributes, Tag.Enum, "orientation-requested-supported", 3, 4, 5, 6);
        AddMissingIntegerAttributes(attributes, Tag.Enum, "print-quality-supported", 3, 4, 5);
        AddMissingRangeAttribute(attributes, "copies-supported", 1, 999);
        AddMissingRangeAttribute(attributes, "job-impressions-supported", 0, int.MaxValue);
        AddMissingRangeAttribute(attributes, "job-media-sheets-supported", 0, int.MaxValue);
        AddMissingRangeAttribute(attributes, "job-pages-per-set-supported", 1, int.MaxValue);
        AddMissingRangeAttribute(attributes, "number-up-supported", 1, 16);
        AddMissingResolutionAttributes(attributes, "printer-resolution-supported");
    }

    private static string GetPrinterDeviceId()
    {
        return string.Concat(
            "MFG:PrintSink;",
            "MDL:E2E IPP Printer;",
            "CMD:PDF,PWGRaster,PCLm;",
            "CLS:PRINTER;",
            "DES:PrintSink E2E IPP Printer;",
            "CID:PrintSinkE2EIPP;");
    }

    private static void AddMissingAttribute(List<IppAttribute> attributes, IppAttribute attribute)
    {
        if (!attributes.Any(existing => string.Equals(existing.Name, attribute.Name, StringComparison.OrdinalIgnoreCase)))
        {
            attributes.Add(attribute);
        }
    }

    private static void AddMissingKeywordAttributes(
        List<IppAttribute> attributes,
        string name,
        params string[] values)
    {
        if (attributes.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (string value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            attributes.Add(new IppAttribute(Tag.Keyword, name, value));
        }
    }

    private static void AddMissingIntegerAttributes(
        List<IppAttribute> attributes,
        Tag tag,
        string name,
        params int[] values)
    {
        if (attributes.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (int value in values.Distinct())
        {
            attributes.Add(new IppAttribute(tag, name, value));
        }
    }

    private static void AddMissingRangeAttribute(
        List<IppAttribute> attributes,
        string name,
        int lower,
        int upper)
    {
        AddMissingAttribute(
            attributes,
            new IppAttribute(
                Tag.RangeOfInteger,
                name,
                new SharpIpp.Protocol.Models.Range(lower, upper, false)));
    }

    private static void AddMissingResolutionAttributes(List<IppAttribute> attributes, string name)
    {
        if (attributes.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        attributes.Add(
            new IppAttribute(
                Tag.Resolution,
                name,
                new Resolution(300, 300, ResolutionUnit.DotsPerInch, true)));
        attributes.Add(
            new IppAttribute(
                Tag.Resolution,
                name,
                new Resolution(600, 600, ResolutionUnit.DotsPerInch, true)));
    }

    private async Task SendErrorResponseAsync(
        IppRequestException exception,
        Stream responseStream,
        CancellationToken cancellationToken)
    {
        IppResponseMessage response = new()
        {
            RequestId = exception.RequestMessage.RequestId,
            Version = exception.RequestMessage.Version,
            StatusCode = exception.StatusCode,
        };
        response.OperationAttributes.Add(
            [
                new IppAttribute(Tag.Charset, "attributes-charset", "utf-8"),
                new IppAttribute(Tag.NaturalLanguage, "attributes-natural-language", "en-us"),
            ]);

        await sharpIppServer.SendRawResponseAsync(response, responseStream, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetExtension(string documentFormat)
    {
        return documentFormat.ToUpperInvariant() switch
        {
            "APPLICATION/PDF" => ".pdf",
            "APPLICATION/PCLM" => ".pclm",
            "IMAGE/PWG-RASTER" => ".pwg",
            "APPLICATION/OXPS" => ".oxps",
            "APPLICATION/VND.MS-XPSDOCUMENT" => ".xps",
            "APPLICATION/POSTSCRIPT" => ".ps",
            _ => ".pdl",
        };
    }
}
