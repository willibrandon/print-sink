namespace PrintSink.Settings;

/// <summary>
/// Provides asynchronous access to user and job settings shared between UI and background tasks.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Reads a string setting.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key is absent.</returns>
    ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes or removes a string setting.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store, or <see langword="null"/> to remove the key.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the setting has been persisted.</returns>
    ValueTask SetStringAsync(string key, string? value, CancellationToken cancellationToken = default);
}
