namespace PrintSink.Endpoints;

using System.Collections.ObjectModel;
using PrintSink.Pdl;

/// <summary>
/// Describes a virtual printer endpoint.
/// </summary>
public sealed class VirtualEndpoint
{
  private readonly HashSet<PdlFormat> supportedPassthroughFormats;

  /// <summary>
  /// Initializes a new instance of the <see cref="VirtualEndpoint" /> class.
  /// </summary>
  /// <param name="kind">The endpoint kind.</param>
  /// <param name="displayName">The display name shown to the user.</param>
  /// <param name="endpointPath">The manifest endpoint path.</param>
  /// <param name="description">A short description of the endpoint.</param>
  /// <param name="preferredInputFormat">The format Windows should render when passthrough is unavailable.</param>
  /// <param name="targetFormat">The output format produced by the endpoint.</param>
  /// <param name="usesSaveAsDialog">A value indicating whether the endpoint expects a Save As target.</param>
  /// <param name="supportedPassthroughFormats">Formats that can be copied without conversion.</param>
  /// <param name="outputFileExtensions">Allowed output file extensions.</param>
  public VirtualEndpoint(
    EndpointKind kind,
    string displayName,
    string endpointPath,
    string description,
    PdlFormat preferredInputFormat,
    PdlFormat targetFormat,
    bool usesSaveAsDialog,
    IEnumerable<PdlFormat> supportedPassthroughFormats,
    IEnumerable<string> outputFileExtensions)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
    ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);
    ArgumentException.ThrowIfNullOrWhiteSpace(description);
    ArgumentNullException.ThrowIfNull(supportedPassthroughFormats);
    ArgumentNullException.ThrowIfNull(outputFileExtensions);

    Kind = kind;
    DisplayName = displayName;
    EndpointPath = endpointPath;
    Description = description;
    PreferredInputFormat = preferredInputFormat;
    TargetFormat = targetFormat;
    UsesSaveAsDialog = usesSaveAsDialog;
    this.supportedPassthroughFormats = new HashSet<PdlFormat>(supportedPassthroughFormats);
    OutputFileExtensions = new ReadOnlyCollection<string>(outputFileExtensions.ToArray());
  }

  /// <summary>
  /// Gets the endpoint kind.
  /// </summary>
  public EndpointKind Kind { get; }

  /// <summary>
  /// Gets the display name shown to the user.
  /// </summary>
  public string DisplayName { get; }

  /// <summary>
  /// Gets the manifest endpoint path.
  /// </summary>
  public string EndpointPath { get; }

  /// <summary>
  /// Gets a short description of the endpoint.
  /// </summary>
  public string Description { get; }

  /// <summary>
  /// Gets the format Windows should render when passthrough is unavailable.
  /// </summary>
  public PdlFormat PreferredInputFormat { get; }

  /// <summary>
  /// Gets the output format produced by the endpoint.
  /// </summary>
  public PdlFormat TargetFormat { get; }

  /// <summary>
  /// Gets a value indicating whether the endpoint expects a Save As target.
  /// </summary>
  public bool UsesSaveAsDialog { get; }

  /// <summary>
  /// Gets the output file extensions supported by the endpoint.
  /// </summary>
  public IReadOnlyList<string> OutputFileExtensions { get; }

  /// <summary>
  /// Determines whether the endpoint can copy a source format without conversion.
  /// </summary>
  /// <param name="format">The source PDL format.</param>
  /// <returns><see langword="true" /> when passthrough is supported.</returns>
  public bool SupportsPassthrough(PdlFormat format) => supportedPassthroughFormats.Contains(format);
}
