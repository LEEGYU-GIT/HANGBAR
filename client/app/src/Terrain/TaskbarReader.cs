using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Terrain;

/// <summary>Finds the taskbar on every monitor: primary via SHAppBarMessage, others via Shell_SecondaryTrayWnd.</summary>
public static class TaskbarReader
{
    public sealed record Taskbar(string MonitorId, RectI Rect, TaskbarEdge Edge, bool AutoHide);

    public static List<Taskbar> Read(MonitorLayout layout)
    {
        var result = new List<Taskbar>();
        var abd = new Win32.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.APPBARDATA>() };
        bool autoHide = ((uint)Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref abd) & Win32.ABS_AUTOHIDE) != 0;

        var primary = Win32.FindWindow("Shell_TrayWnd", null);
        if (primary != 0 && Win32.SHAppBarMessage(Win32.ABM_GETTASKBARPOS, ref abd) != 0)
            Add(result, layout, primary, DisplayInfo.ToRect(abd.rc), autoHide);

        nint w = 0;
        while ((w = Win32.FindWindowEx(0, w, "Shell_SecondaryTrayWnd", null)) != 0)
            if (Win32.GetWindowRect(w, out var r)) Add(result, layout, w, DisplayInfo.ToRect(r), autoHide);

        return result;
    }

    static void Add(List<Taskbar> list, MonitorLayout layout, nint hwnd, RectI rect, bool autoHide)
    {
        var mon = layout.Monitors.FirstOrDefault(m => m.Bounds.Contains(new PointI(rect.X + rect.W / 2, rect.Y + rect.H / 2)));
        if (mon is null) return;
        var b = mon.Bounds;
        TaskbarEdge edge = rect.W >= rect.H
            ? (rect.Y + rect.H / 2 > b.Y + b.H / 2 ? TaskbarEdge.Bottom : TaskbarEdge.Top)
            : (rect.X + rect.W / 2 < b.X + b.W / 2 ? TaskbarEdge.Left : TaskbarEdge.Right);
        list.Add(new Taskbar(mon.Id, rect, edge, autoHide));
    }
}
