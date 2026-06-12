using PrintSink.Watermark;

namespace PrintSink.Settings;

/// <summary>
/// Persists effective watermark options through an <see cref="ISettingsStore"/>.
/// </summary>
public sealed class WatermarkSettingsService
{
    /// <summary>
    /// The key used for job watermark settings.
    /// </summary>
    public const string WatermarkOptionsKey = "PrintSink.WatermarkOptions";

    private readonly ISettingsStore settingsStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatermarkSettingsService"/> class.
    /// </summary>
    /// <param name="settingsStore">The backing settings store.</param>
    public WatermarkSettingsService(ISettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);

        this.settingsStore = settingsStore;
    }

    /// <summary>
    /// Loads watermark options.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored options, or disabled options when no value is present.</returns>
    public async ValueTask<WatermarkOptions> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? json = await settingsStore.GetStringAsync(WatermarkOptionsKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? WatermarkOptions.Disabled : WatermarkOptions.FromJson(json);
    }

    /// <summary>
    /// Saves watermark options.
    /// </summary>
    /// <param name="options">The options to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the options have been persisted.</returns>
    public ValueTask SaveAsync(WatermarkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? json = options.IsEnabled ? options.ToJson() : null;
        return settingsStore.SetStringAsync(WatermarkOptionsKey, json, cancellationToken);
    }
}
