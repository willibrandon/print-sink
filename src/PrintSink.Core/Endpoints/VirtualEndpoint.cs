using PrintSink.Core.Pdl;

namespace PrintSink.Core.Endpoints;

/// <summary>
/// Describes a virtual printer endpoint exposed by PrintSink.
/// </summary>
public sealed class VirtualEndpoint
{
    private readonly HashSet<PdlFormat> passthroughFormats;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualEndpoint"/> class.
    /// </summary>
    /// <param name="kind">The endpoint kind.</param>
    /// <param name="queueName">The Windows queue display name.</param>
    /// <param name="printerUri">The stable printer URI used to resolve the endpoint.</param>
    /// <param name="preferredInputFormat">The manifest preferred input format.</param>
    /// <param name="targetFormat">The endpoint output format.</param>
    /// <param name="passthroughFormats">The source formats accepted without conversion.</param>
    /// <param name="requiresTargetFile">A value indicating whether the endpoint writes to an OS-selected target file.</param>
    /// <param name="defaultExtension">The default output extension, or <see langword="null"/> for non-file sinks.</param>
    /// <param name="outputExtensions">The output extensions accepted by the OS Save-As broker.</param>
    public VirtualEndpoint(
        EndpointKind kind,
        string queueName,
        Uri printerUri,
        PdlFormat preferredInputFormat,
        PdlFormat targetFormat,
        IEnumerable<PdlFormat> passthroughFormats,
        bool requiresTargetFile,
        string? defaultExtension,
        IEnumerable<string>? outputExtensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(printerUri);
        ArgumentNullException.ThrowIfNull(passthroughFormats);

        string? normalizedDefaultExtension = string.IsNullOrWhiteSpace(defaultExtension)
            ? null
            : NormalizeExtension(defaultExtension);
        string[] normalizedOutputExtensions = NormalizeOutputExtensions(normalizedDefaultExtension, outputExtensions);
        if (requiresTargetFile && normalizedOutputExtensions.Length == 0)
        {
            throw new ArgumentException("File-backed endpoints must declare at least one output extension.", nameof(outputExtensions));
        }

        if (!requiresTargetFile && normalizedOutputExtensions.Length > 0)
        {
            throw new ArgumentException("Application sinks must not declare output extensions.", nameof(outputExtensions));
        }

        Kind = kind;
        QueueName = queueName;
        PrinterUri = printerUri;
        PreferredInputFormat = preferredInputFormat;
        TargetFormat = targetFormat;
        this.passthroughFormats = [.. passthroughFormats];
        PassthroughFormats = [.. this.passthroughFormats];
        RequiresTargetFile = requiresTargetFile;
        DefaultExtension = normalizedDefaultExtension;
        OutputExtensions = normalizedOutputExtensions;
    }

    /// <summary>
    /// Gets the endpoint kind.
    /// </summary>
    public EndpointKind Kind { get; }

    /// <summary>
    /// Gets the Windows queue display name.
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Gets the stable printer URI used to resolve the endpoint.
    /// </summary>
    public Uri PrinterUri { get; }

    /// <summary>
    /// Gets the manifest preferred input format.
    /// </summary>
    public PdlFormat PreferredInputFormat { get; }

    /// <summary>
    /// Gets the endpoint output format.
    /// </summary>
    public PdlFormat TargetFormat { get; }

    /// <summary>
    /// Gets the source formats accepted without conversion.
    /// </summary>
    public IReadOnlyCollection<PdlFormat> PassthroughFormats { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint writes to an OS-selected target file.
    /// </summary>
    public bool RequiresTargetFile { get; }

    /// <summary>
    /// Gets the default output extension, or <see langword="null"/> for non-file sinks.
    /// </summary>
    public string? DefaultExtension { get; }

    /// <summary>
    /// Gets the output extensions accepted by the OS Save-As broker.
    /// </summary>
    public IReadOnlyList<string> OutputExtensions { get; }

    /// <summary>
    /// Returns whether the endpoint accepts a source format without conversion.
    /// </summary>
    /// <param name="format">The source format.</param>
    /// <returns><see langword="true"/> when passthrough is supported; otherwise, <see langword="false"/>.</returns>
    public bool SupportsPassthrough(PdlFormat format)
    {
        return passthroughFormats.Contains(format);
    }

    private static string[] NormalizeOutputExtensions(string? defaultExtension, IEnumerable<string>? outputExtensions)
    {
        IEnumerable<string> source = outputExtensions ?? (defaultExtension is null ? [] : [defaultExtension]);
        return [.. source
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string NormalizeExtension(string extension)
    {
        string trimmedExtension = extension.Trim();
        return trimmedExtension.StartsWith('.')
            ? trimmedExtension
            : string.Concat(".", trimmedExtension);
    }
}
