namespace PrintSink.Core.Settings;

/// <summary>
/// Resolves the shared package-local settings directory used by foreground and background components.
/// </summary>
public static class PackagedSettingsDirectory
{
    /// <summary>
    /// Gets the PrintSink settings directory below a packaged app's local folder.
    /// </summary>
    /// <param name="localFolderPath">The package-local folder path.</param>
    /// <returns>The directory used for PrintSink settings files.</returns>
    public static string GetRootDirectory(string localFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFolderPath);

        return Path.Combine(localFolderPath, "Settings");
    }
}
