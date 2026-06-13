namespace PrintSink.Xps.Projections;

internal sealed class XpsGenerationFailureTracker
{
    internal ulong? Error { get; private set; }

    internal void Record(object? _, ulong error)
    {
        Error = error;
    }
}
