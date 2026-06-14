using System.Runtime.InteropServices;
using Windows.Devices.Printers;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;
using WinRT;

namespace PrintSink.App;

internal static unsafe class UniversalApiContract19PrinterApis
{
    private const int ErrorNoInterface = unchecked((int)0x80004002);
    private const string PdlPassthroughProviderType = "Windows.Devices.Printers.PdlPassthroughProvider";

    internal static bool TryCreateSupportedPdlPassthroughProvider2Reference(
        PdlPassthroughProvider provider,
        out IObjectReference? provider2Reference,
        out string providerDetail)
    {
        ArgumentNullException.ThrowIfNull(provider);

        provider2Reference = null;
        if (!ApiInformation.IsPropertyPresent(PdlPassthroughProviderType, "IsPassthroughWithJobAttributesSupported")
            || !ApiInformation.IsMethodPresent(PdlPassthroughProviderType, "StartPrintJobWithIppJobAttributes"))
        {
            providerDetail = "pdlPassthroughProvider=v1; provider2=unavailable; provider2Submit=fallback-v1";
            return false;
        }

        int queryResult = QueryInterface(
            provider,
            ABI.Windows.Devices.Printers.IPdlPassthroughProvider2Methods.IID,
            out IntPtr provider2Ptr);
        if (queryResult == ErrorNoInterface)
        {
            providerDetail = "pdlPassthroughProvider=v1; provider2=unavailable; provider2Submit=fallback-v1";
            return false;
        }

        try
        {
            Marshal.ThrowExceptionForHR(queryResult);
            provider2Reference = ComWrappersSupport.GetObjectReferenceForInterface(
                provider2Ptr,
                ABI.Windows.Devices.Printers.IPdlPassthroughProvider2Methods.IID,
                false);
            if (!ABI.Windows.Devices.Printers.IPdlPassthroughProvider2Methods
                .get_IsPassthroughWithJobAttributesSupported(provider2Reference))
            {
                providerDetail = "pdlPassthroughProvider=v1; provider2=unsupported; provider2Submit=fallback-v1";
                provider2Reference.Dispose();
                provider2Reference = null;
                return false;
            }

            providerDetail = "pdlPassthroughProvider=v1; provider2=supported";
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            providerDetail = $"pdlPassthroughProvider=v1; provider2=runtime-unusable; provider2ProbeHResult=0x{ex.HResult:X8}; provider2Submit=fallback-v1";
            provider2Reference?.Dispose();
            provider2Reference = null;
            return false;
        }
        finally
        {
            if (provider2Ptr != IntPtr.Zero)
            {
                Marshal.Release(provider2Ptr);
            }
        }
    }

    internal static PdlPassthroughTarget StartPrintJobWithIppJobAttributes(
        IObjectReference provider2Reference,
        string jobName,
        string pdlContentType,
        IBuffer jobAttributes,
        IBuffer operationAttributes)
    {
        ArgumentNullException.ThrowIfNull(provider2Reference);

        return ABI.Windows.Devices.Printers.IPdlPassthroughProvider2Methods.StartPrintJobWithIppJobAttributes(
            provider2Reference,
            jobName,
            pdlContentType,
            jobAttributes,
            operationAttributes);
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
