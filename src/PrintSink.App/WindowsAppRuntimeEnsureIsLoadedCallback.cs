using System.Runtime.InteropServices;

namespace PrintSink.App;

/// <summary>
/// Calls the Windows App Runtime self-contained initialization export.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int WindowsAppRuntimeEnsureIsLoadedCallback();
