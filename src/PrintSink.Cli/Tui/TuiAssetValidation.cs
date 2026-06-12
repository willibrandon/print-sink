namespace PrintSink.Cli.Tui;

internal sealed class TuiAssetValidation
{
    internal TuiAssetValidation(
        string name,
        string path,
        bool succeeded,
        IReadOnlyList<string> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(messages);

        Name = name;
        Path = path;
        Succeeded = succeeded;
        Messages = messages;
    }

    internal string Name { get; }

    internal string Path { get; }

    internal bool Succeeded { get; }

    internal IReadOnlyList<string> Messages { get; }
}
