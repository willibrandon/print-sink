using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PrintSink.App;

internal static class WindowsAppSdkUndockedRegFreeWinRTInitializer
{
    private const string WindowsAppRuntimeFileName = "Microsoft.WindowsAppRuntime.dll";
    private const string WindowsAppRuntimeEnsureIsLoadedExport = "WindowsAppRuntime_EnsureIsLoaded";

    [ModuleInitializer]
    internal static void AccessWindowsAppSDK()
    {
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

        string windowsAppRuntimePath = Path.Combine(AppContext.BaseDirectory, WindowsAppRuntimeFileName);
        nint windowsAppRuntime = NativeLibrary.Load(windowsAppRuntimePath);
        nint ensureIsLoadedExport = NativeLibrary.GetExport(windowsAppRuntime, WindowsAppRuntimeEnsureIsLoadedExport);
        WindowsAppRuntimeEnsureIsLoadedCallback ensureIsLoaded =
            Marshal.GetDelegateForFunctionPointer<WindowsAppRuntimeEnsureIsLoadedCallback>(ensureIsLoadedExport);

        _ = ensureIsLoaded();
    }
}
