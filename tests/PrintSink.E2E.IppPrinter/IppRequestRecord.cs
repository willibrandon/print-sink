namespace PrintSink.E2E.IppPrinter;

internal sealed class IppRequestRecord
{
    internal IppRequestRecord(
        DateTimeOffset timestamp,
        string operation,
        IReadOnlyList<string> operationAttributes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> operationAttributeValues,
        IReadOnlyList<string> jobAttributes,
        IReadOnlyList<string> responsePrinterAttributes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> responsePrinterAttributeValues)
    {
        Timestamp = timestamp;
        Operation = operation;
        OperationAttributes.AddRange(operationAttributes);
        foreach (KeyValuePair<string, IReadOnlyList<string>> attribute in operationAttributeValues)
        {
            OperationAttributeValues[attribute.Key] = [.. attribute.Value];
        }

        JobAttributes.AddRange(jobAttributes);
        ResponsePrinterAttributes.AddRange(responsePrinterAttributes);
        foreach (KeyValuePair<string, IReadOnlyList<string>> attribute in responsePrinterAttributeValues)
        {
            ResponsePrinterAttributeValues[attribute.Key] = [.. attribute.Value];
        }
    }

    internal DateTimeOffset Timestamp { get; }

    internal string Operation { get; }

    internal List<string> OperationAttributes { get; } = [];

    internal Dictionary<string, List<string>> OperationAttributeValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal List<string> JobAttributes { get; } = [];

    internal List<string> ResponsePrinterAttributes { get; } = [];

    internal Dictionary<string, List<string>> ResponsePrinterAttributeValues { get; } = new(StringComparer.OrdinalIgnoreCase);
}
