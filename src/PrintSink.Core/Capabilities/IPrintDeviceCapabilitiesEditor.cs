using System.Xml.Linq;

namespace PrintSink.Core.Capabilities;

/// <summary>
/// Applies PrintSink feature additions to Print Device Capabilities XML.
/// </summary>
public interface IPrintDeviceCapabilitiesEditor
{
    /// <summary>
    /// Applies the supplied feature additions to a Print Device Capabilities document.
    /// </summary>
    /// <param name="document">The source Print Device Capabilities document.</param>
    /// <param name="features">The feature additions to apply.</param>
    /// <returns>A new Print Device Capabilities document containing the additions.</returns>
    XDocument Apply(XDocument document, IReadOnlyList<CustomFeature> features);
}
