using PrintSink.Core.Abstractions;
using Windows.Graphics.Printing.PrintTicket;

namespace PrintSink.Tasks;

internal sealed class WinRtPrintTicket : IPrintTicket
{
    internal WinRtPrintTicket(WorkflowPrintTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Xml = ticket.XmlNode.GetXml();
    }

    /// <inheritdoc />
    public string Xml { get; }
}
