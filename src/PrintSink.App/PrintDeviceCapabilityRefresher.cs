using System.Runtime.ExceptionServices;
using Windows.Devices.Printers;

namespace PrintSink.App;

/// <summary>
/// Refreshes printer capabilities through a bounded worker call.
/// </summary>
internal static class PrintDeviceCapabilityRefresher
{
    internal static void Refresh(string printerName, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        ExceptionDispatchInfo? exception = null;
        using ManualResetEventSlim completed = new();
        Thread thread = new(() =>
        {
            try
            {
                IppPrintDevice printDevice = IppPrintDevice.FromPrinterName(printerName);
                printDevice.RefreshPrintDeviceCapabilities();
            }
            catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = $"PrintSink capabilities: {printerName}",
        };

        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();

        if (!completed.Wait(timeout))
        {
            throw new TimeoutException(
                $"RefreshPrintDeviceCapabilities for {printerName} did not complete within {timeout.TotalSeconds:0} seconds.");
        }

        exception?.Throw();
    }
}
