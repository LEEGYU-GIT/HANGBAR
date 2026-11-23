namespace Hangbar.Core;

/// <summary>Point in physical pixels on the virtual desktop.</summary>
public readonly record struct PointI(int X, int Y);

/// <summary>Rectangle in physical pixels on the virtual desktop. Right and Bottom are exclusive.</summary>
public readonly record struct RectI(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;

    public bool Contains(PointI p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

    /// <summary>0 inside; otherwise Chebyshev distance to the nearest edge.</summary>
    public int DistanceTo(PointI p)
    {
        int dx = Math.Max(Math.Max(X - p.X, 0), p.X - Right);
        int dy = Math.Max(Math.Max(Y - p.Y, 0), p.Y - Bottom);
        return Contains(p) ? 0 : Math.Max(1, Math.Max(dx, dy)); // a point on the exclusive edge is 1 away, not 0
    }
}

/// <summary>A display. All coordinates are physical pixels on the virtual desktop.</summary>
public sealed record Monitor(string Id, RectI Bounds, RectI WorkArea, double Scale, bool Primary);

public sealed record MonitorChange(IReadOnlyList<Monitor> Added, IReadOnlyList<Monitor> Removed, IReadOnlyList<Monitor> Changed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}

public sealed record MonitorLayout(IReadOnlyList<Monitor> Monitors)
{
    public static MonitorChange Diff(MonitorLayout before, MonitorLayout after)
    {
        var old = before.Monitors.ToDictionary(m => m.Id);
        var now = after.Monitors.ToDictionary(m => m.Id);
        var added = after.Monitors.Where(m => !old.ContainsKey(m.Id)).ToList();
        var removed = before.Monitors.Where(m => !now.ContainsKey(m.Id)).ToList();
        var changed = after.Monitors
            .Where(m => old.TryGetValue(m.Id, out var o)
                        && (o.Bounds != m.Bounds || o.WorkArea != m.WorkArea || o.Scale != m.Scale))
            .ToList();
        return new MonitorChange(added, removed, changed);
    }
}

public static class OverlayGeometry
{
    /// <summary>
    /// One pixel shorter than the monitor, so Windows does not treat the always-on-top
    /// overlay as a fullscreen app (design §6-2).
    /// </summary>
    public static RectI OverlayRectFor(Monitor m) => m.Bounds with { H = m.Bounds.H - 1 };
}
