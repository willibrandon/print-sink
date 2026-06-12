using System.Collections.Concurrent;

namespace PrintSink.Settings;

/// <summary>
/// Stores settings in memory for tests and non-packaged adapters.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        values.TryGetValue(key, out string? value);
        return ValueTask.FromResult(value);
    }

    /// <inheritdoc />
    public ValueTask SetStringAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (value is null)
        {
            values.TryRemove(key, out _);
        }
        else
        {
            values[key] = value;
        }

        return ValueTask.CompletedTask;
    }
}
