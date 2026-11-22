using Godot;
using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>
/// Once a second, decides whether something fullscreen is in front and hides all overlays if so
/// (design §6-3). Hidden overlays are really hidden (not just transparent) and the frame rate drops.
/// </summary>
public partial class FullscreenWatcher : Node
{
    static readonly HashSet<string> ShellClasses = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
    const int NormalFps = 30, HiddenFps = 5;

    OverlayManager _overlays = null!;
    ClickThroughDriver _click = null!;
    Func<IEnumerable<nint>> _ownWindows = null!;
    double _since = 1.0;

    public bool Hidden { get; private set; }
    public event Action<bool>? HiddenChanged;

    public void Init(OverlayManager overlays, ClickThroughDriver click, Func<IEnumerable<nint>> ownWindows)
    {
        _overlays = overlays;
        _click = click;
        _ownWindows = ownWindows;
    }

    public override void _Process(double delta)
    {
        _since += delta;
        if (_since < 1.0) return;
        _since = 0;

        bool hide = Evaluate();
        if (hide == Hidden) return;
        Hidden = hide;
        _overlays.SetAllVisible(!hide);
        _click.Paused = hide;
        Engine.MaxFps = hide ? HiddenFps : NormalFps;
        HiddenChanged?.Invoke(hide);
    }

    bool Evaluate()
    {
        var state = Win32.SHQueryUserNotificationState(out int s) == 0 ? (QunsState)s : QunsState.AcceptsNotifications;
        var fg = Win32.GetForegroundWindow();
        if (fg == 0) return FullscreenPolicy.ShouldHide(state, null, default, false);

        bool shellOrOwn = ShellClasses.Contains(Win32.ClassName(fg)) || _ownWindows().Contains(fg);
        RectI? fgRect = Win32.TryFrameBounds(fg, out var r) ? DisplayInfo.ToRect(r) : null;

        var mon = Win32.MonitorFromWindow(fg, Win32.MONITOR_DEFAULTTONEAREST);
        var info = new Win32.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFOEX>() };
        var monRect = Win32.GetMonitorInfo(mon, ref info) ? DisplayInfo.ToRect(info.rcMonitor) : default;

        return FullscreenPolicy.ShouldHide(state, fgRect, monRect, shellOrOwn);
    }
}
