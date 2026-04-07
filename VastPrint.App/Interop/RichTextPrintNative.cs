using System.Runtime.InteropServices;

namespace VastPrint.App.Interop;

internal static class RichTextPrintNative
{
    private const int WmUser = 0x0400;
    private const int EmFormatRange = WmUser + 57;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr SendFormatRange(IntPtr handle, bool render, IntPtr formatRangePointer)
    {
        return SendMessage(handle, EmFormatRange, new IntPtr(render ? 1 : 0), formatRangePointer);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CharRange
    {
        public int CpMin;
        public int CpMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FormatRange
    {
        public IntPtr Hdc;
        public IntPtr HdcTarget;
        public Rect Rc;
        public Rect RcPage;
        public CharRange Chrg;
    }
}
