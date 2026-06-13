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
            RecordRequest(rawRequest);

            IIppResponse response = await CreateResponseAsync(rawRequest, request, cancellationToken)
                .ConfigureAwait(false);
            IIppResponseMessage rawResponse = await sharpIppServer
                .CreateRawResponseAsync(response, cancellationToken)
                .ConfigureAwait(false);
            ImproveRawResponse(request, rawResponse);

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
            File.AppendAllText(
                options.ErrorLogPath,
                DateTimeOffset.UtcNow.ToString("O") + Environment.NewLine + ex + Environment.NewLine);
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
                PrinterIsAcceptingJobs = true,
                PrinterMakeAndModel = "PrintSink E2E IPP Printer",
                PrinterName = options.PrinterName,
                PrinterState = GetPrinterState(),
                PrinterStateReasons = [PrinterStateReason.None],
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
            StatusCode = IppStatusCode.SuccessfulOk,
        };
    }

    private CreateJobResponse CreateCreateJobResponse(
        IIppRequestMessage rawRequest,
        CreateJobRequest request)
    {
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
        if (job is null)
        {
            return new SendDocumentResponse
            {
                RequestId = request.RequestId,
                Version = request.Version,
                StatusCode = IppStatusCode.ClientErrorNotPossible,
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

    private static IIppResponse CreateUnsupportedResponse(IIppRequest request)
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
        await using (FileStream output = File.Create(documentPath))
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
        return GetQueuedJobCount() > 0 ? PrinterState.Processing : PrinterState.Idle;
    }

    private int GetQueuedJobCount()
    {
        lock (gate)
        {
            return jobs.Count(job => job.State == "pending");
        }
    }

    private void RecordRequest(IIppRequestMessage rawRequest)
    {
        lock (gate)
        {
            requests.Add(
                new IppRequestRecord(
                    DateTimeOffset.UtcNow,
                    rawRequest.IppOperation.ToString(),
                    AttributeNames(rawRequest.OperationAttributes),
                    AttributeNames(rawRequest.JobAttributes)));
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
    }

    private string GetPrinterDeviceId()
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
        return documentFormat.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/pclm" => ".pclm",
            "image/pwg-raster" => ".pwg",
            "application/oxps" => ".oxps",
            "application/vnd.ms-xpsdocument" => ".xps",
            "application/postscript" => ".ps",
            _ => ".pdl",
        };
    }
}
