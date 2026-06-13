using System.Xml.Linq;

namespace PrintSink.Core.Capabilities;

/// <summary>
/// Describes a prefixed XML name used by Print Schema documents.
/// </summary>
public sealed class PrintSchemaQualifiedName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrintSchemaQualifiedName"/> class.
    /// </summary>
    /// <param name="prefix">The XML namespace prefix to preserve in generated documents.</param>
    /// <param name="namespaceUri">The XML namespace URI.</param>
    /// <param name="localName">The XML local name.</param>
    public PrintSchemaQualifiedName(string prefix, string namespaceUri, string localName)
        : this(prefix, new Uri(namespaceUri, UriKind.Absolute), localName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrintSchemaQualifiedName"/> class.
    /// </summary>
    /// <param name="prefix">The XML namespace prefix to preserve in generated documents.</param>
    /// <param name="namespaceUri">The XML namespace URI.</param>
    /// <param name="localName">The XML local name.</param>
    public PrintSchemaQualifiedName(string prefix, Uri namespaceUri, string localName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(namespaceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);

        Prefix = prefix;
        NamespaceUri = namespaceUri;
        LocalName = localName;
    }

    /// <summary>
    /// Gets the XML namespace prefix to preserve in generated documents.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the XML namespace URI.
    /// </summary>
    public Uri NamespaceUri { get; }

    /// <summary>
    /// Gets the XML local name.
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// Creates a Print Schema keyword name.
    /// </summary>
    /// <param name="localName">The keyword local name.</param>
    /// <returns>The qualified name.</returns>
    public static PrintSchemaQualifiedName Keyword(string localName)
    {
        return new PrintSchemaQualifiedName("psk", PrintSchemaNamespaces.Keywords, localName);
    }

    /// <summary>
    /// Creates a Print Schema keyword v1.2 name.
    /// </summary>
    /// <param name="localName">The keyword local name.</param>
    /// <returns>The qualified name.</returns>
    public static PrintSchemaQualifiedName Keyword12(string localName)
    {
        return new PrintSchemaQualifiedName("psk12", PrintSchemaNamespaces.Keywords12, localName);
    }

    /// <summary>
    /// Creates a PrintSink custom keyword name.
    /// </summary>
    /// <param name="localName">The custom local name.</param>
    /// <returns>The qualified name.</returns>
    public static PrintSchemaQualifiedName PrintSink(string localName)
    {
        return new PrintSchemaQualifiedName("printsink", "https://schemas.printsink.dev/printing/keywords", localName);
    }

    internal XName ToXName()
    {
        return XNamespace.Get(NamespaceUri.AbsoluteUri) + LocalName;
    }
}
