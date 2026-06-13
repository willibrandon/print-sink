namespace PrintSink.Tasks;

internal static class BackgroundTaskExceptionPolicy
{
    internal static bool IsRecoverable(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException;
    }
}
