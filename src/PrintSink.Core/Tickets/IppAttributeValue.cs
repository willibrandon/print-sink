namespace PrintSink.Tickets;

/// <summary>
/// Represents a print-stack-neutral IPP attribute value.
/// </summary>
public sealed class IppAttributeValue
{
    private readonly byte[]? binaryValue;
    private readonly string[] stringValues;

    private IppAttributeValue(string name, string syntax, IEnumerable<string> stringValues, byte[]? binaryValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(syntax);
        ArgumentNullException.ThrowIfNull(stringValues);

        Name = name;
        Syntax = syntax;
        this.stringValues = stringValues.ToArray();
        this.binaryValue = binaryValue?.ToArray();
    }

    /// <summary>
    /// Gets the IPP attribute name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the IPP syntax name.
    /// </summary>
    public string Syntax { get; }

    /// <summary>
    /// Gets string values for keyword, name, integer-as-text, and collection-shaped attributes.
    /// </summary>
    public IReadOnlyList<string> StringValues => stringValues;

    /// <summary>
    /// Gets a value indicating whether this attribute contains binary data.
    /// </summary>
    public bool HasBinaryValue => binaryValue is not null;

    /// <summary>
    /// Gets a copy of the binary value.
    /// </summary>
    /// <returns>The binary value, or <see langword="null"/>.</returns>
    public byte[]? GetBinaryValue()
    {
        return binaryValue?.ToArray();
    }

    /// <summary>
    /// Creates a single string-valued attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="syntax">The IPP syntax name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>The attribute value object.</returns>
    public static IppAttributeValue CreateString(string name, string syntax, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new IppAttributeValue(name, syntax, new[] { value }, null);
    }

    /// <summary>
    /// Creates a multi-value string attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="syntax">The IPP syntax name.</param>
    /// <param name="values">The attribute values.</param>
    /// <returns>The attribute value object.</returns>
    public static IppAttributeValue CreateStrings(string name, string syntax, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new IppAttributeValue(name, syntax, values, null);
    }

    /// <summary>
    /// Creates a binary attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="syntax">The IPP syntax name.</param>
    /// <param name="value">The binary value.</param>
    /// <returns>The attribute value object.</returns>
    public static IppAttributeValue Binary(string name, string syntax, ReadOnlySpan<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value.Length);

        return new IppAttributeValue(name, syntax, Array.Empty<string>(), value.ToArray());
    }
}
