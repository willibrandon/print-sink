namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes an IPP job attribute produced from a print ticket.
/// </summary>
public sealed class IppAttributeValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IppAttributeValue"/> class.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="values">The attribute values.</param>
    public IppAttributeValue(string name, IEnumerable<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        Name = name;
        Values = [.. values];

        if (Values.Count == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }
    }

    /// <summary>
    /// Gets the IPP attribute name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the attribute values.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Creates a single-valued IPP attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>The IPP attribute.</returns>
    public static IppAttributeValue Single(string name, string value)
    {
        return new IppAttributeValue(name, [value]);
    }
}
