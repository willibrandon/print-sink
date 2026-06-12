namespace PrintSink.Core.Tickets;

/// <summary>
/// Describes a member to remove from an IPP collection attribute before job submission.
/// </summary>
public sealed class IppCollectionMemberRemoval
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IppCollectionMemberRemoval"/> class.
    /// </summary>
    /// <param name="attributeName">The IPP collection attribute name.</param>
    /// <param name="memberName">The collection member name to remove.</param>
    public IppCollectionMemberRemoval(string attributeName, string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        AttributeName = attributeName;
        MemberName = memberName;
    }

    /// <summary>
    /// Gets the IPP collection attribute name.
    /// </summary>
    public string AttributeName { get; }

    /// <summary>
    /// Gets the collection member name to remove.
    /// </summary>
    public string MemberName { get; }
}
