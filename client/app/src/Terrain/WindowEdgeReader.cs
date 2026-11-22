using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Terrain;

/// <summary>
/// Lists visible top-level windows front-most first with their visible frame bounds.
/// Reads positions and class names only — never titles or contents (design §3, §6-4).
/// </summary>
public static class WindowEdgeReader
{
    static readonly HashSet<string> Skip = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    public static List<(nint Hwnd, RectI Rect)> Read(IReadOnlySet<nint> own)
    {
        var list = new List<(nint, RectI)>();
        Win32.EnumWindows((h, _) =>
        {
            if (own.Contains(h) || !IsStandable(h)) return true;
            if (!Win32.TryFrameBounds(h, out var r)) return true;
            var rect = DisplayInfo.ToRect(r);
            if (rect.W > 0 && rect.H > 0) list.Add((h, rect));
            return true;
        }, 0);
        return list;
    }

    /// <summary>True while a window is something a character can stand on.</summary>
    public static bool IsStandable(nint h)
    {
        if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h) || Win32.IsCloaked(h)) return false;
        long ex = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE);
        if ((ex & Win32.WS_EX_TOOLWINDOW) != 0) return false;
        return !Skip.Contains(Win32.ClassName(h));
    }
}
