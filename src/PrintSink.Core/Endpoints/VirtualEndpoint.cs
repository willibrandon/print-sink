using PrintSink.Pdl;

namespace PrintSink.Endpoints;

/// <summary>
/// Describes a virtual printer endpoint declared by the package manifest.
/// </summary>
public sealed class VirtualEndpoint
{
    private readonly HashSet<PdlFormat> supportedPassthroughFormats;
    private readonly string[] outputFileExtensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualEndpoint"/> class.
    /// </summary>
    /// <param name="kind">The endpoint kind.</param>
    /// <param name="queueResourceName">The localized resource key for the queue display name.</param>
    /// <param name="endpointPath">The endpoint path component of the printer address.</param>
    /// <param name="preferredInputFormat">The preferred PDL requested from the print system.</param>
    /// <param name="targetFormat">The endpoint output format.</param>
    /// <param name="usesSaveAsDialog">Whether the endpoint declares OutputFileTypes and receives a target file.</param>
    /// <param name="supportedPassthroughFormats">Formats accepted without OS re-rendering.</param>
    /// <param name="outputFileExtensions">File extensions offered by the Save As broker.</param>
    public VirtualEndpoint(
        EndpointKind kind,
        string queueResourceName,
        string endpointPath,
        PdlFormat preferredInputFormat,
        PdlFormat targetFormat,
        bool usesSaveAsDialog,
        IEnumerable<PdlFormat> supportedPassthroughFormats,
        IEnumerable<string> outputFileExtensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueResourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);
        ArgumentNullException.ThrowIfNull(supportedPassthroughFormats);
        ArgumentNullException.ThrowIfNull(outputFileExtensions);

        Kind = kind;
        QueueResourceName = queueResourceName;
        EndpointPath = NormalizeEndpointPath(endpointPath);
        PreferredInputFormat = preferredInputFormat;
        TargetFormat = targetFormat;
        UsesSaveAsDialog = usesSaveAsDialog;
        this.supportedPassthroughFormats = new HashSet<PdlFormat>(supportedPassthroughFormats);
        this.outputFileExtensions = outputFileExtensions.Select(NormalizeExtension).ToArray();
    }

    /// <summary>
    /// Gets the endpoint kind.
    /// </summary>
    public EndpointKind Kind { get; }

    /// <summary>
    /// Gets the localized resource key for the queue display name.
    /// </summary>
    public string QueueResourceName { get; }

    /// <summary>
    /// Gets the endpoint path component of the printer address.
    /// </summary>
    public string EndpointPath { get; }

    /// <summary>
    /// Gets the preferred input format declared in the MSIX manifest.
    /// </summary>
    public PdlFormat PreferredInputFormat { get; }

    /// <summary>
    /// Gets the output format produced by this endpoint.
    /// </summary>
    public PdlFormat TargetFormat { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint uses the OS Save As broker.
    /// </summary>
    public bool UsesSaveAsDialog { get; }

    /// <summary>
    /// Gets the supported passthrough formats.
    /// </summary>
    public IReadOnlySet<PdlFormat> SupportedPassthroughFormats => supportedPassthroughFormats;

    /// <summary>
    /// Gets the file extensions offered by the Save As broker.
    /// </summary>
    public IReadOnlyList<string> OutputFileExtensions => outputFileExtensions;

    /// <summary>
    /// Gets a value indicating whether the endpoint writes to a user-selected file.
    /// </summary>
    public bool IsFileBacked => UsesSaveAsDialog;

    /// <summary>
    /// Returns whether the endpoint accepts a format without OS re-rendering.
    /// </summary>
    /// <param name="format">The format to inspect.</param>
    /// <returns><see langword="true"/> when passthrough is supported.</returns>
    public bool SupportsPassthrough(PdlFormat format)
    {
        return supportedPassthroughFormats.Contains(format);
    }

    private static string NormalizeEndpointPath(string endpointPath)
    {
        string trimmed = endpointPath.Trim();
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
