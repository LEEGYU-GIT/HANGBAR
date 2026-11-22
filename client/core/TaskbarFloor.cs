namespace Hangbar.Core;

public enum TaskbarEdge { Bottom, Top, Left, Right }

/// <summary>Where characters stand on a monitor: on top of a bottom taskbar, otherwise on the monitor's bottom edge.</summary>
public static class TaskbarFloor
{
    public static int FloorY(Monitor m, RectI? taskbarRect, TaskbarEdge edge, bool autoHide)
    {
        if (taskbarRect is { } tb && edge == TaskbarEdge.Bottom && !autoHide) return tb.Y;
        return m.Bounds.Bottom - 1;
    }
}
