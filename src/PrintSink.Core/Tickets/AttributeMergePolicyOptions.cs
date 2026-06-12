namespace PrintSink.Tickets;

/// <summary>
/// Provides options for mapping print ticket values into IPP job attributes.
/// </summary>
public sealed class AttributeMergePolicyOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeMergePolicyOptions"/> class.
    /// </summary>
    /// <param name="mergePolicy">The merge policy to request from the workflow adapter.</param>
    /// <param name="removeMediaSize">Whether media-size attributes should be removed because size travels in the PDL header.</param>
    /// <param name="includeCopies">Whether copy count should be mapped.</param>
    public AttributeMergePolicyOptions(IppAttributeMergePolicy mergePolicy, bool removeMediaSize, bool includeCopies)
    {
        MergePolicy = mergePolicy;
        RemoveMediaSize = removeMediaSize;
        IncludeCopies = includeCopies;
    }

    /// <summary>
    /// Gets the default mapping options for PrintSink.
    /// </summary>
    public static AttributeMergePolicyOptions Default { get; } = new(IppAttributeMergePolicy.Replace, removeMediaSize: true, includeCopies: true);

    /// <summary>
    /// Gets the merge policy to request from the workflow adapter.
    /// </summary>
    public IppAttributeMergePolicy MergePolicy { get; }

    /// <summary>
    /// Gets a value indicating whether media-size attributes should be removed.
    /// </summary>
    public bool RemoveMediaSize { get; }

    /// <summary>
    /// Gets a value indicating whether copy count should be mapped.
    /// </summary>
    public bool IncludeCopies { get; }
}
