namespace PrintSink.E2E.IppPrinter;

internal sealed class IppRequestRecord
{
    internal IppRequestRecord(
        DateTimeOffset timestamp,
        string operation,
        IReadOnlyList<string> operationAttributes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> operationAttributeValues,
        IReadOnlyList<string> jobAttributes)
    {
        Timestamp = timestamp;
        Operation = operation;
        OperationAttributes.AddRange(operationAttributes);
        foreach (KeyValuePair<string, IReadOnlyList<string>> attribute in operationAttributeValues)
        {
            OperationAttributeValues[attribute.Key] = [.. attribute.Value];
        }

        JobAttributes.AddRange(jobAttributes);
    }

    internal DateTimeOffset Timestamp { get; }

    internal string Operation { get; }

    internal List<string> OperationAttributes { get; } = [];

    internal Dictionary<string, List<string>> OperationAttributeValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal List<string> JobAttributes { get; } = [];
}
