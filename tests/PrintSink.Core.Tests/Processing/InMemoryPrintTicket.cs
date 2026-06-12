using PrintSink.Core.Abstractions;

namespace PrintSink.Core.Tests.Processing;

/// <summary>
/// Provides an in-memory print-ticket fixture.
/// </summary>
internal sealed class InMemoryPrintTicket : IPrintTicket
{
    internal InMemoryPrintTicket(string xml)
    {
        Xml = xml;
    }

    /// <inheritdoc />
    public string Xml { get; }
}
