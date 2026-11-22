namespace Hangbar.Core;

/// <summary>QUERY_USER_NOTIFICATION_STATE values from SHQueryUserNotificationState (Win32 values as-is).</summary>
public enum QunsState
{
    NotPresent = 1,
    Busy = 2,
    RunningD3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

/// <summary>Decides whether overlays should hide because something fullscreen is in front (design §6-3).</summary>
public static class FullscreenPolicy
{
    public static bool ShouldHide(QunsState state, RectI? foregroundRect, RectI foregroundMonitorBounds, bool foregroundIsShellOrOwn)
    {
        if (state is QunsState.Busy or QunsState.RunningD3DFullScreen or QunsState.PresentationMode) return true;
        if (foregroundIsShellOrOwn || foregroundRect is not { } r) return false;
        var m = foregroundMonitorBounds;
        // Borderless "windowed fullscreen" games are missed by QUNS; cover-the-whole-monitor catches them.
        return r.X <= m.X && r.Y <= m.Y && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }
}
