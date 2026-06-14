using System.Runtime.InteropServices;
using Windows.Foundation.Metadata;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Graphics.Printing.Workflow;
using WinRT;

namespace PrintSink.Tasks;

internal static unsafe class UniversalApiContract19PrintSupport
{
    private const int InterfaceMethodStart = 6;
    private const int ErrorNoInterface = unchecked((int)0x80004002);
    private const string CapabilitiesChangedEventArgsType =
        "Windows.Graphics.Printing.PrintSupport.PrintSupportPrintDeviceCapabilitiesChangedEventArgs";
    private const string PrintWorkflowPrinterJobType =
        "Windows.Graphics.Printing.Workflow.PrintWorkflowPrinterJob";

    private static readonly Guid PrintDeviceCapabilitiesChangedEventArgs5InterfaceId =
        new("bc72f631-8177-5ef0-94c1-929080525b5a");
    private static readonly Guid PrintWorkflowPrinterJob3InterfaceId =
        new("f0c8eeec-66ac-5e14-8906-0de610769368");

    internal static string EnablePdlPassthroughWithJobAttributes(
        PrintSupportPrintDeviceCapabilitiesChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!ApiInformation.IsMethodPresent(
            CapabilitiesChangedEventArgsType,
            "SetPdlPassthroughWithJobAttributesSupported"))
        {
            return "pdlPassthroughWithJobAttributes=unavailable";
        }

        int queryResult = QueryInterface(args, PrintDeviceCapabilitiesChangedEventArgs5InterfaceId, out IntPtr thisPtr);
        if (queryResult == ErrorNoInterface)
        {
            return "pdlPassthroughWithJobAttributes=unavailable";
        }

        try
        {
            Marshal.ThrowExceptionForHR(queryResult);
            IntPtr* vtable = *(IntPtr**)thisPtr;
            WriteBoolean(thisPtr, vtable[InterfaceMethodStart], true);
            return "pdlPassthroughWithJobAttributes=enabled";
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return $"pdlPassthroughWithJobAttributes=error; pdlPassthroughWithJobAttributesError=0x{ex.HResult:X8}";
        }
        finally
        {
            if (thisPtr != IntPtr.Zero)
            {
                Marshal.Release(thisPtr);
            }
        }
    }

    internal static string GetWorkflowPassthroughWithAttributesDetail(PrintWorkflowPrinterJob printerJob)
    {
        ArgumentNullException.ThrowIfNull(printerJob);

        if (!ApiInformation.IsPropertyPresent(PrintWorkflowPrinterJobType, "IsPassthroughJobWithAttributes"))
        {
            return "passthroughWithAttributes=unavailable";
        }

        int queryResult = QueryInterface(printerJob, PrintWorkflowPrinterJob3InterfaceId, out IntPtr thisPtr);
        if (queryResult == ErrorNoInterface)
        {
            return "passthroughWithAttributes=unavailable";
        }

        try
        {
            Marshal.ThrowExceptionForHR(queryResult);
            IntPtr* vtable = *(IntPtr**)thisPtr;
            if (!ReadBoolean(thisPtr, vtable[InterfaceMethodStart]))
            {
                return "passthroughWithAttributes=false";
            }

            string jobAttributes = ReadAttributeMapState(thisPtr, vtable[InterfaceMethodStart + 1]);
            string operationAttributes = ReadAttributeMapState(thisPtr, vtable[InterfaceMethodStart + 2]);
            return string.Join(
                "; ",
                "passthroughWithAttributes=true",
                $"passthroughJobAttributes={jobAttributes}",
                $"passthroughOperationAttributes={operationAttributes}");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return $"passthroughWithAttributes=error; passthroughWithAttributesError=0x{ex.HResult:X8}";
        }
        finally
        {
            if (thisPtr != IntPtr.Zero)
            {
                Marshal.Release(thisPtr);
            }
        }
    }

    private static string ReadAttributeMapState(IntPtr thisPtr, IntPtr methodPointer)
    {
        var getAttributes = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)methodPointer;
        IntPtr attributes = IntPtr.Zero;
        try
        {
            int result = getAttributes(thisPtr, &attributes);
            if (result < 0)
            {
                return $"error=0x{result:X8}";
            }

            return attributes == IntPtr.Zero ? "absent" : "present";
        }
        finally
        {
            if (attributes != IntPtr.Zero)
            {
                Marshal.Release(attributes);
            }
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

    private static void WriteBoolean(IntPtr thisPtr, IntPtr methodPointer, bool value)
    {
        var setValue = (delegate* unmanaged[Stdcall]<IntPtr, byte, int>)methodPointer;
        Marshal.ThrowExceptionForHR(setValue(thisPtr, value ? (byte)1 : (byte)0));
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
