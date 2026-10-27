using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using XBar.Core;

namespace XBar.App;
internal static class MonitorAreas
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct INFO { public int Size; public RECT Monitor, Work; public uint Flags; }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref INFO info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    public static void KeepVisible(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var current)) return;
        // Win32 window and monitor work areas are both physical pixels in PerMonitorV2.
        // Dividing each monitor's absolute origin by its own DPI corrupts mixed-DPI layouts.
        var work = new List<PixelBounds>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr h, ref RECT r, IntPtr d) => {
            var info = new INFO { Size = Marshal.SizeOf<INFO>() };
            if (GetMonitorInfo(m, ref info)) work.Add(new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top)); return true;
        }, IntPtr.Zero);
        var original = new PixelBounds(current.Left, current.Top, current.Right - current.Left, current.Bottom - current.Top);
        var safe = WindowPlacement.EnsureVisible(original, work);
        if (safe != original) SetWindowPos(handle, IntPtr.Zero, safe.X, safe.Y, safe.Width, safe.Height, 0x0014); // no activation or z-order change
    }
}
