using System.Text.Json;

namespace PrintSink.E2E.IppPrinter;

internal sealed class IppPrinterEvidenceWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string outputDirectory;

    internal IppPrinterEvidenceWriter(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        this.outputDirectory = outputDirectory;
    }

    internal string EvidencePath => Path.Combine(outputDirectory, "ipp-jobs.json");

    internal void Write(
        IppPrinterOptions options,
        IReadOnlyList<IppPrinterJob> jobs,
        IReadOnlyList<IppRequestRecord> requests)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(requests);

        Directory.CreateDirectory(outputDirectory);
        var evidence = new
        {
            printerName = options.PrinterName,
            printerUri = options.PrinterUri.ToString(),
            documentFormat = options.DocumentFormat,
            writtenAt = DateTimeOffset.UtcNow,
            jobs = jobs.Select(static job => new
            {
                id = job.Id,
                name = job.Name,
                createdUtc = job.CreatedUtc,
                completedUtc = job.CompletedUtc,
                state = job.State,
                documentFormat = job.DocumentFormat,
                documentPath = job.DocumentPath,
                documentBytes = job.DocumentBytes,
                operationPasswordCollectionReceived = job.OperationPasswordCollectionReceived,
                operationAttributes = job.OperationAttributes.Order(StringComparer.OrdinalIgnoreCase),
                jobAttributes = job.JobAttributes.Order(StringComparer.OrdinalIgnoreCase),
            }),
            requests = requests.Select(static request => new
            {
                timestamp = request.Timestamp,
                operation = request.Operation,
                operationAttributes = request.OperationAttributes.Order(StringComparer.OrdinalIgnoreCase),
                operationAttributeValues = request.OperationAttributeValues.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.Order(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase),
                jobAttributes = request.JobAttributes.Order(StringComparer.OrdinalIgnoreCase),
            }),
        };

        File.WriteAllText(EvidencePath, JsonSerializer.Serialize(evidence, SerializerOptions));
    }
}
