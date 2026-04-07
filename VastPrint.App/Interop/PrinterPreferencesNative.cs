using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace VastPrint.App.Interop;

internal static class PrinterPreferencesNative
{
    private const int DmOutBuffer = 0x2;
    private const int DmInPrompt = 0x4;
    private const int DmInBuffer = 0x8;
    private const int IdOk = 1;

    public static bool ShowDialog(IntPtr ownerHandle, PrintDocument document)
    {
        var printerName = document.PrinterSettings.PrinterName;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
        {
            return false;
        }

        var devModeHandle = document.PrinterSettings.GetHdevmode(document.DefaultPageSettings);
        if (devModeHandle == IntPtr.Zero)
        {
            ClosePrinter(printerHandle);
            return false;
        }

        try
        {
            var devModePointer = GlobalLock(devModeHandle);
            if (devModePointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var result = DocumentProperties(
                    ownerHandle,
                    printerHandle,
                    printerName,
                    devModePointer,
                    devModePointer,
                    DmInBuffer | DmOutBuffer | DmInPrompt);

                if (result != IdOk)
                {
                    return false;
                }
            }
            finally
            {
                GlobalUnlock(devModeHandle);
            }

            document.PrinterSettings.SetHdevmode(devModeHandle);
            document.DefaultPageSettings.SetHdevmode(devModeHandle);
            return true;
        }
        finally
        {
            GlobalFree(devModeHandle);
            ClosePrinter(printerHandle);
        }
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode)]
    private static extern int DocumentProperties(
        IntPtr hwnd,
        IntPtr hPrinter,
        string pDeviceName,
        IntPtr pDevModeOutput,
        IntPtr pDevModeInput,
        int fMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
