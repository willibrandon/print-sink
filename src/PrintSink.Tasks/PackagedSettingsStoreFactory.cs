using PrintSink.Core.Settings;
using Windows.Storage;

namespace PrintSink.Tasks;

/// <summary>
/// Creates background-task settings stores backed by packaged local storage.
/// </summary>
internal static class PackagedSettingsStoreFactory
{
    /// <summary>
    /// Creates a settings store for the current packaged app identity.
    /// </summary>
    /// <returns>The local settings store.</returns>
    internal static LocalSettingsStore Create()
    {
        return new LocalSettingsStore(GetRootDirectory());
    }

    /// <summary>
    /// Gets the app-local directory used for persisted PrintSink settings.
    /// </summary>
    /// <returns>The root settings directory.</returns>
    internal static string GetRootDirectory()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, "Settings");
    }
}
