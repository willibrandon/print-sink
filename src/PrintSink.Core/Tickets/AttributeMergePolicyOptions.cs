namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes attribute removals applied before a print job is submitted.
/// </summary>
public sealed class AttributeMergePolicyOptions
{
    private readonly HashSet<string> attributesToRemove;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeMergePolicyOptions"/> class.
    /// </summary>
    /// <param name="attributesToRemove">IPP attribute names to remove.</param>
    public AttributeMergePolicyOptions(IEnumerable<string> attributesToRemove)
    {
        ArgumentNullException.ThrowIfNull(attributesToRemove);

        this.attributesToRemove = new HashSet<string>(attributesToRemove, StringComparer.OrdinalIgnoreCase);
        AttributesToRemove = [.. this.attributesToRemove];
    }

    /// <summary>
    /// Gets a policy that removes no attributes.
    /// </summary>
    public static AttributeMergePolicyOptions None { get; } = new([]);

    /// <summary>
    /// Gets the IPP attribute names to remove.
    /// </summary>
    public IReadOnlyCollection<string> AttributesToRemove { get; }

    /// <summary>
    /// Returns whether the attribute should be removed.
    /// </summary>
    /// <param name="attributeName">The IPP attribute name.</param>
    /// <returns><see langword="true"/> when the attribute should be removed; otherwise, <see langword="false"/>.</returns>
    public bool ShouldRemove(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return attributesToRemove.Contains(attributeName);
    }
}
