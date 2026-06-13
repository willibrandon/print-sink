using Microsoft.UI.Reactor;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics.Printing.PrintSupport;
using WinRT.Interop;

namespace PrintSink.App;

internal static partial class SettingsWindowOwner
{
    private const int GwlParentWindow = -8;
    private const uint GetAncestorRoot = 2;

    internal static string Apply(ReactorWindow? window, PrintSupportSettingsActivatedEventArgs? settingsArgs)
    {
        if (window is null)
        {
            return "Settings window unavailable.";
        }

        if (settingsArgs is null)
        {
            return "Owner window unavailable.";
        }

        nint ownerHwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(ToMicrosoftWindowId(settingsArgs.OwnerWindowId));
        if (ownerHwnd == 0)
        {
            return "Owner HWND unavailable.";
        }

        nint rootOwnerHwnd = GetAncestor(ownerHwnd, GetAncestorRoot);
        if (rootOwnerHwnd != 0)
        {
            ownerHwnd = rootOwnerHwnd;
        }

        nint childHwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
        if (childHwnd == 0)
        {
            childHwnd = WindowNative.GetWindowHandle(window.NativeWindow);
        }

        if (childHwnd == 0)
        {
            return "Settings HWND unavailable.";
        }

        _ = SetWindowLongPtr(childHwnd, GwlParentWindow, ownerHwnd);
        // Reactor creates this Window before the PSA owner is known; disable the owner directly.
        DisableOwnerUntilClosed(window.NativeWindow, ownerHwnd);

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            return "Modal to print preferences owner.";
        }

        return "Owned by print preferences window.";
    }

    internal static Microsoft.UI.WindowId ToMicrosoftWindowId(Windows.UI.WindowId windowId)
    {
        Microsoft.UI.WindowId result;
        result.Value = windowId.Value;
        return result;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint windowHandle, int index, nint newLong);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetAncestor(nint windowHandle, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnableWindow(nint windowHandle, [MarshalAs(UnmanagedType.Bool)] bool enable);

    private static void DisableOwnerUntilClosed(Window settingsWindow, nint ownerHwnd)
    {
        _ = EnableWindow(ownerHwnd, false);
        settingsWindow.Closed += (_, _) => EnableWindow(ownerHwnd, true);
    }
}
