using System.Xml.Linq;

namespace PrintSink.Capabilities;

/// <summary>
/// Applies PrintSink custom features to Print Device Capabilities XML.
/// </summary>
public interface IPrintDeviceCapabilitiesEditor
{
    /// <summary>
    /// Applies custom features to Print Device Capabilities XML.
    /// </summary>
    /// <param name="capabilities">The existing Print Device Capabilities document.</param>
    /// <param name="features">The custom features to inject or replace.</param>
    /// <returns>A new Print Device Capabilities document.</returns>
    XDocument Apply(XDocument capabilities, IReadOnlyList<CustomFeature> features);
}
