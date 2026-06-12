using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PrintSink.Xps.Projections;

/// <summary>
/// Activates the native XPS WinRT side-by-side manifest for unpackaged callers.
/// </summary>
internal sealed partial class NativeXpsActivationContext : IDisposable
{
    private static readonly nint InvalidHandle = new(-1);

    private readonly nint context;
    private readonly nint cookie;

    private NativeXpsActivationContext(nint context, nint cookie)
    {
        this.context = context;
        this.cookie = cookie;
    }

    internal static NativeXpsActivationContext Activate()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "PrintSink.Xps.dll.manifest");
        if (!File.Exists(manifestPath))
        {
            return new NativeXpsActivationContext(0, 0);
        }

        unsafe
        {
            fixed (char* manifestPathPointer = manifestPath)
            {
                ActivationContext context = new()
                {
                    Size = Marshal.SizeOf<ActivationContext>(),
                    Source = (nint)manifestPathPointer,
                };

                nint contextHandle = CreateActCtx(ref context);
                if (contextHandle == InvalidHandle)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (!ActivateActCtx(contextHandle, out nint cookie))
                {
                    int error = Marshal.GetLastWin32Error();
                    ReleaseActCtx(contextHandle);
                    throw new Win32Exception(error);
                }

                return new NativeXpsActivationContext(contextHandle, cookie);
            }
        }
    }

    /// <summary>
    /// Deactivates the side-by-side activation context and releases its native handle.
    /// </summary>
    public void Dispose()
    {
        if (cookie != 0)
        {
            DeactivateActCtx(0, cookie);
        }

        if (context != 0)
        {
            ReleaseActCtx(context);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateActCtxW", SetLastError = true)]
    private static partial nint CreateActCtx(ref ActivationContext context);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ActivateActCtx(nint context, out nint cookie);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeactivateActCtx(uint flags, nint cookie);

    [LibraryImport("kernel32.dll")]
    private static partial void ReleaseActCtx(nint context);

}
