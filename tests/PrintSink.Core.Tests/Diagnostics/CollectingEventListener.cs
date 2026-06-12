using System.Diagnostics.Tracing;

namespace PrintSink.Core.Tests.Diagnostics;

internal sealed class CollectingEventListener : EventListener
{
    private readonly List<string> eventNames = [];
    private readonly object gate = new();

    internal IReadOnlyList<string> EventNames
    {
        get
        {
            lock (gate)
            {
                return [.. eventNames];
            }
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != "PrintSink-Diagnostics" || eventData.EventName is null)
        {
            return;
        }

        lock (gate)
        {
            eventNames.Add(eventData.EventName);
        }
    }
}
