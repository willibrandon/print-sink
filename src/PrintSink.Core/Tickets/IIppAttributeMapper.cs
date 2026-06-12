using System.Xml.Linq;

namespace PrintSink.Core.Tickets;

/// <summary>
/// Maps print-ticket XML into IPP job attributes.
/// </summary>
public interface IIppAttributeMapper
{
    /// <summary>
    /// Maps print-ticket XML into IPP job attributes.
    /// </summary>
    /// <param name="printTicket">The print-ticket XML document.</param>
    /// <returns>The mapped IPP attributes.</returns>
    IReadOnlyDictionary<string, IppAttributeValue> FromPrintTicket(XDocument printTicket);

    /// <summary>
    /// Applies merge policy removals to mapped IPP attributes.
    /// </summary>
    /// <param name="attributes">The source IPP attributes.</param>
    /// <param name="options">The merge policy options.</param>
    /// <returns>A new attribute map with removals applied.</returns>
    IReadOnlyDictionary<string, IppAttributeValue> ApplyMergePolicy(
        IReadOnlyDictionary<string, IppAttributeValue> attributes,
        AttributeMergePolicyOptions options);
}
