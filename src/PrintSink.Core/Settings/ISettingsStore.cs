using PrintSink.Core.Watermark;

namespace PrintSink.Core.Settings;

/// <summary>
/// Persists user-configured processing settings shared by foreground UI and background jobs.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Gets watermark settings for a virtual printer.
    /// </summary>
    /// <param name="printerUri">The virtual printer URI.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The persisted watermark settings, or disabled settings when none exist.</returns>
    Task<WatermarkOptions> GetWatermarkOptionsAsync(Uri printerUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves watermark settings for a virtual printer.
    /// </summary>
    /// <param name="printerUri">The virtual printer URI.</param>
    /// <param name="options">The watermark settings.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the settings are saved.</returns>
    Task SaveWatermarkOptionsAsync(Uri printerUri, WatermarkOptions options, CancellationToken cancellationToken = default);
}
