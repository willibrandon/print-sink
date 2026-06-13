using System.Globalization;
using System.Xml.Linq;
using PrintSink.Core.Endpoints;
using Windows.Data.Xml.Dom;
using Windows.Devices.Printers;
using Windows.Graphics.Printing.PrintTicket;

namespace PrintSink.App;

/// <summary>
/// Edits user default print tickets through the package-identity printer API.
/// </summary>
internal static class UserDefaultPrintTicketEditor
{
    private static readonly XNamespace Psf = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
    private static readonly XNamespace Psk = "http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    internal static async Task<string> SetCopiesAsync(
        EndpointKind endpointKind,
        int copies,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(copies);

        VirtualEndpoint endpoint = EndpointCatalog.GetByKind(endpointKind);
        IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(endpoint.QueueName);
        if (!printDevice.CanModifyUserDefaultPrintTicket)
        {
            throw new InvalidOperationException($"{endpoint.QueueName} does not allow user default print ticket changes.");
        }

        WorkflowPrintTicket currentTicket = printDevice.UserDefaultPrintTicket;
        XDocument document = XDocument.Parse(currentTicket.XmlNode.GetXml(), LoadOptions.PreserveWhitespace);
        SetCopies(document, copies);

        XmlDocument xmlDocument = currentTicket.XmlNode as XmlDocument
            ?? currentTicket.XmlNode.OwnerDocument
            ?? throw new InvalidOperationException("The default print ticket XML document is unavailable.");
        xmlDocument.LoadXml(document.ToString(SaveOptions.DisableFormatting));
        await currentTicket.NotifyXmlChangedAsync().AsTask(cancellationToken).ConfigureAwait(false);

        WorkflowPrintTicketValidationResult validationResult = await currentTicket
            .ValidateAsync()
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        if (!validationResult.Validated)
        {
            throw new InvalidOperationException("The updated default print ticket did not validate.");
        }

        printDevice.UserDefaultPrintTicket = currentTicket;

        int? verifiedCopies = ReadCopies(printDevice.UserDefaultPrintTicket);
        return $"User default print ticket updated for {endpoint.QueueName}: copies={copies}; verifiedCopies={verifiedCopies?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}";
    }

    private static void SetCopies(XDocument document, int copies)
    {
        XElement root = document.Root
            ?? throw new ArgumentException("Print ticket XML does not have a root element.", nameof(document));

        EnsureNamespace(root, "psf", Psf);
        EnsureNamespace(root, "psk", Psk);
        EnsureNamespace(root, "xsd", Xsd);
        EnsureNamespace(root, "xsi", Xsi);

        XElement? initializer = root
            .Elements(Psf + "ParameterInit")
            .FirstOrDefault(static element =>
                string.Equals(
                    (string?)element.Attribute("name"),
                    "psk:JobCopiesAllDocuments",
                    StringComparison.Ordinal));
        if (initializer is null)
        {
            initializer = new XElement(Psf + "ParameterInit", new XAttribute("name", "psk:JobCopiesAllDocuments"));
            root.Add(initializer);
        }

        XElement? value = initializer.Element(Psf + "Value");
        if (value is null)
        {
            value = new XElement(Psf + "Value");
            initializer.Add(value);
        }

        value.SetAttributeValue(Xsi + "type", "xsd:integer");
        value.Value = copies.ToString(CultureInfo.InvariantCulture);
    }

    private static int? ReadCopies(WorkflowPrintTicket ticket)
    {
        XDocument document = XDocument.Parse(ticket.XmlNode.GetXml(), LoadOptions.PreserveWhitespace);
        XElement? initializer = document
            .Root?
            .Elements(Psf + "ParameterInit")
            .FirstOrDefault(static element =>
                string.Equals(
                    (string?)element.Attribute("name"),
                    "psk:JobCopiesAllDocuments",
                    StringComparison.Ordinal));
        string? value = initializer?.Element(Psf + "Value")?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int copies)
            ? copies
            : null;
    }

    private static void EnsureNamespace(XElement root, string prefix, XNamespace namespaceName)
    {
        XName attributeName = XNamespace.Xmlns + prefix;
        if (root.Attribute(attributeName) is null)
        {
            root.SetAttributeValue(attributeName, namespaceName.NamespaceName);
        }
    }
}
