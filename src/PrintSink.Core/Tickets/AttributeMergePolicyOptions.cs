namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes attribute removals applied before a print job is submitted.
/// </summary>
public sealed class AttributeMergePolicyOptions
{
    private readonly HashSet<string> attributesToRemove;
    private readonly List<IppCollectionMemberRemoval> collectionMemberRemovals;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeMergePolicyOptions"/> class.
    /// </summary>
    /// <param name="attributesToRemove">IPP attribute names to remove.</param>
    public AttributeMergePolicyOptions(IEnumerable<string> attributesToRemove)
        : this(attributesToRemove, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeMergePolicyOptions"/> class.
    /// </summary>
    /// <param name="attributesToRemove">IPP attribute names to remove.</param>
    /// <param name="collectionMemberRemovals">IPP collection members to remove.</param>
    public AttributeMergePolicyOptions(
        IEnumerable<string> attributesToRemove,
        IEnumerable<IppCollectionMemberRemoval> collectionMemberRemovals)
    {
        ArgumentNullException.ThrowIfNull(attributesToRemove);
        ArgumentNullException.ThrowIfNull(collectionMemberRemovals);

        this.attributesToRemove = new HashSet<string>(attributesToRemove, StringComparer.OrdinalIgnoreCase);
        this.collectionMemberRemovals = [.. collectionMemberRemovals];
        AttributesToRemove = [.. this.attributesToRemove];
        CollectionMemberRemovals = [.. this.collectionMemberRemovals];
    }

    /// <summary>
    /// Gets a policy that removes no attributes.
    /// </summary>
    public static AttributeMergePolicyOptions None { get; } = new([]);

    /// <summary>
    /// Gets the default policy for PDLs that already carry media size in their own header.
    /// </summary>
    public static AttributeMergePolicyOptions RemovePdlEmbeddedMediaSize { get; } = new(
        [],
        [new IppCollectionMemberRemoval("media-col", "media-size")]);

    /// <summary>
    /// Gets the IPP attribute names to remove.
    /// </summary>
    public IReadOnlyCollection<string> AttributesToRemove { get; }

    /// <summary>
    /// Gets the IPP collection members to remove.
    /// </summary>
    public IReadOnlyCollection<IppCollectionMemberRemoval> CollectionMemberRemovals { get; }

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

    /// <summary>
    /// Returns whether the collection member should be removed.
    /// </summary>
    /// <param name="attributeName">The IPP collection attribute name.</param>
    /// <param name="memberName">The collection member name.</param>
    /// <returns><see langword="true"/> when the member should be removed; otherwise, <see langword="false"/>.</returns>
    public bool ShouldRemoveCollectionMember(string attributeName, string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        return collectionMemberRemovals.Any(removal =>
            string.Equals(removal.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(removal.MemberName, memberName, StringComparison.OrdinalIgnoreCase));
    }
}
