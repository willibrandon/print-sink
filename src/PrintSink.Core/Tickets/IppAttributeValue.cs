namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes an IPP job attribute produced from a print ticket.
/// </summary>
public sealed class IppAttributeValue
{
    private IppAttributeValue(
        string name,
        IReadOnlyList<string> values,
        IReadOnlyList<IReadOnlyDictionary<string, IppAttributeValue>> collections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Values = values;
        Collections = collections;
    }

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
        Collections = [];

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
    /// Gets collection values when this attribute represents an IPP collection.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, IppAttributeValue>> Collections { get; }

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

    /// <summary>
    /// Creates a single IPP collection attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="members">The collection members.</param>
    /// <returns>The IPP collection attribute.</returns>
    public static IppAttributeValue Collection(
        string name,
        IReadOnlyDictionary<string, IppAttributeValue> members)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(members);

        return CollectionArray(name, [members]);
    }

    /// <summary>
    /// Creates an IPP collection-array attribute.
    /// </summary>
    /// <param name="name">The IPP attribute name.</param>
    /// <param name="collections">The collection values.</param>
    /// <returns>The IPP collection-array attribute.</returns>
    public static IppAttributeValue CollectionArray(
        string name,
        IEnumerable<IReadOnlyDictionary<string, IppAttributeValue>> collections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(collections);

        List<IReadOnlyDictionary<string, IppAttributeValue>> materialized = [.. collections];
        if (materialized.Count == 0)
        {
            throw new ArgumentException("At least one collection is required.", nameof(collections));
        }

        return new IppAttributeValue(name, [], materialized);
    }
}
