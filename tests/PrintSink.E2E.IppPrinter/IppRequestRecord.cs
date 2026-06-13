namespace PrintSink.E2E.IppPrinter;

internal sealed class IppRequestRecord
{
    internal IppRequestRecord(
        DateTimeOffset timestamp,
        string operation,
        IReadOnlyList<string> operationAttributes,
        IReadOnlyList<string> jobAttributes)
    {
        Timestamp = timestamp;
        Operation = operation;
        OperationAttributes.AddRange(operationAttributes);
        JobAttributes.AddRange(jobAttributes);
    }

    internal DateTimeOffset Timestamp { get; }

    internal string Operation { get; }

    internal List<string> OperationAttributes { get; } = [];

    internal List<string> JobAttributes { get; } = [];
}
