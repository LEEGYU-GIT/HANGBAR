namespace Hangbar.Core;

public readonly record struct ClickThroughDecision(bool Passthrough, int NextPollMs);

/// <summary>
/// Decides, from a polled cursor position, whether an overlay should let clicks through
/// and how soon to poll again (design §6-2: cursor polling, no global hooks).
/// </summary>
public static class ClickThroughPolicy
{
    public const int NearPx = 200;
    public const int NearPollMs = 33;
    public const int FarPollMs = 100;

    public static ClickThroughDecision Decide(PointI cursor, IReadOnlyList<RectI> hitRects)
    {
        int nearest = int.MaxValue;
        foreach (var r in hitRects)
            nearest = Math.Min(nearest, r.DistanceTo(cursor));

        if (nearest == 0) return new ClickThroughDecision(false, NearPollMs);
        if (nearest <= NearPx) return new ClickThroughDecision(true, NearPollMs);
        return new ClickThroughDecision(true, FarPollMs);
    }
}
