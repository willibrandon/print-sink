namespace PrintSink.E2E.IppPrinter;

internal sealed class IppPrinterJob
{
    internal IppPrinterJob(
        int id,
        string? name,
        DateTimeOffset createdUtc,
        IReadOnlyList<string> operationAttributes,
        IReadOnlyList<string> jobAttributes)
    {
        Id = id;
        Name = name;
        CreatedUtc = createdUtc;
        OperationAttributes.AddRange(operationAttributes);
        JobAttributes.AddRange(jobAttributes);
    }

    internal int Id { get; }

    internal string? Name { get; set; }

    internal DateTimeOffset CreatedUtc { get; }

    internal DateTimeOffset? CompletedUtc { get; set; }

    internal string State { get; set; } = "pending";

    internal string? DocumentFormat { get; set; }

    internal string? DocumentPath { get; set; }

    internal long DocumentBytes { get; set; }

    internal List<string> OperationAttributes { get; } = [];

    internal List<string> JobAttributes { get; } = [];

    internal bool OperationPasswordCollectionReceived =>
        OperationAttributes.Contains("msft-operation-attribute-col", StringComparer.OrdinalIgnoreCase)
        || OperationAttributes.Contains("job-password", StringComparer.OrdinalIgnoreCase)
        || OperationAttributes.Contains("job-password-encryption", StringComparer.OrdinalIgnoreCase)
        || JobAttributes.Contains("msft-operation-attribute-col", StringComparer.OrdinalIgnoreCase);
}
