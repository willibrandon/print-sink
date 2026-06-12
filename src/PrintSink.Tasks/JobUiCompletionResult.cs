namespace PrintSink.Tasks;

internal readonly struct JobUiCompletionResult
{
    internal JobUiCompletionResult(bool shouldProcess, bool usedForegroundUi)
    {
        ShouldProcess = shouldProcess;
        UsedForegroundUi = usedForegroundUi;
    }

    internal bool ShouldProcess { get; }

    internal bool UsedForegroundUi { get; }
}
