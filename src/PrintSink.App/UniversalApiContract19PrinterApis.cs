using System.Runtime.InteropServices;
using Windows.Devices.Printers;
using Windows.Foundation.Metadata;
using WinRT;

namespace PrintSink.App;

internal static unsafe partial class UniversalApiContract19PrinterApis
{
    private const int InterfaceMethodStart = 6;
    private const int ErrorNoInterface = unchecked((int)0x80004002);
    private const string PdlPassthroughProviderType = "Windows.Devices.Printers.PdlPassthroughProvider";

    private static readonly Guid PdlPassthroughProvider2InterfaceId =
        new("7330305c-a17d-52ec-a129-9a4ff9c8f655");

    internal static string GetPdlPassthroughWithJobAttributesDetail(PdlPassthroughProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!ApiInformation.IsPropertyPresent(PdlPassthroughProviderType, "IsPassthroughWithJobAttributesSupported")
            || !ApiInformation.IsMethodPresent(PdlPassthroughProviderType, "StartPrintJobWithIppJobAttributes"))
        {
            return "pdlPassthroughProvider=v1; provider2=unavailable";
        }

        int queryResult = QueryInterface(provider, PdlPassthroughProvider2InterfaceId, out IntPtr thisPtr);
        if (queryResult == ErrorNoInterface)
        {
            return "pdlPassthroughProvider=v1; provider2=unavailable";
        }

        try
        {
            Marshal.ThrowExceptionForHR(queryResult);
            IntPtr* vtable = *(IntPtr**)thisPtr;
            string provider2 = ReadBoolean(thisPtr, vtable[InterfaceMethodStart]) ? "supported" : "unsupported";
            return $"pdlPassthroughProvider=v1; provider2={provider2}; provider2Submit=projection-unavailable";
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return $"pdlPassthroughProvider=v1; provider2=error; provider2Error=0x{ex.HResult:X8}";
        }
        finally
        {
            Marshal.Release(thisPtr);
        }
    }

    private static bool ReadBoolean(IntPtr thisPtr, IntPtr methodPointer)
    {
        IntPtr valuePointer = Marshal.AllocHGlobal(sizeof(byte));
        try
        {
            Marshal.WriteByte(valuePointer, 0);
            var getValue = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)methodPointer;
            Marshal.ThrowExceptionForHR(getValue(thisPtr, valuePointer));
            return Marshal.ReadByte(valuePointer) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(valuePointer);
        }
    }

    private static int QueryInterface(object instance, Guid interfaceId, out IntPtr thisPtr)
    {
        thisPtr = IntPtr.Zero;
        ObjectReferenceValue objectReference = MarshalInspectable<object>.CreateMarshaler2(instance);
        try
        {
            IntPtr instancePtr = MarshalInspectable<object>.GetAbi(objectReference);
            if (instancePtr == IntPtr.Zero)
            {
                return ErrorNoInterface;
            }

            Guid requestedInterface = interfaceId;
            return Marshal.QueryInterface(instancePtr, in requestedInterface, out thisPtr);
        }
        finally
        {
            MarshalInspectable<object>.DisposeMarshaler(objectReference);
        }
    }
}
