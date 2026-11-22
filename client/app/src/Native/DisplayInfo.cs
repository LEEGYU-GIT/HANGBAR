using Hangbar.Core;

namespace Hangbar.App.Native;

/// <summary>Reads the current monitor layout in physical pixels.</summary>
internal static class DisplayInfo
{
    public static MonitorLayout Read()
    {
        var monitors = new List<Hangbar.Core.Monitor>();
        Win32.EnumDisplayMonitors(0, 0, (nint h, nint _, ref Win32.RECT _, nint _) =>
        {
            var info = new Win32.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFOEX>() };
            if (!Win32.GetMonitorInfo(h, ref info)) return true;
            double scale = Win32.GetDpiForMonitor(h, Win32.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 ? dpi / 96.0 : 1.0;
            monitors.Add(new Hangbar.Core.Monitor(
                info.szDevice,
                ToRect(info.rcMonitor),
                ToRect(info.rcWork),
                scale,
                (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0));
            return true;
        }, 0);
        return new MonitorLayout(monitors);
    }

    public static RectI ToRect(Win32.RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
