namespace PrintSink.Core.Capabilities;

/// <summary>
/// Describes a property element added under a Print Schema option.
/// </summary>
public sealed class PrintSchemaProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrintSchemaProperty"/> class.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="kind">The Print Schema property kind.</param>
    /// <param name="value">The property value.</param>
    /// <param name="xsiType">The optional XML Schema instance type, such as <c>xsd:integer</c>.</param>
    public PrintSchemaProperty(
        PrintSchemaQualifiedName name,
        PrintSchemaPropertyKind kind,
        string value,
        string? xsiType = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Name = name;
        Kind = kind;
        Value = value;
        XsiType = xsiType;
    }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public PrintSchemaQualifiedName Name { get; }

    /// <summary>
    /// Gets the Print Schema property kind.
    /// </summary>
    public PrintSchemaPropertyKind Kind { get; }

    /// <summary>
    /// Gets the property value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the optional XML Schema instance type, such as <c>xsd:integer</c>.
    /// </summary>
    public string? XsiType { get; }
}
