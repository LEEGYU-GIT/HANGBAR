namespace Hangbar.Core;

/// <summary>A walkable piece of a window's top edge: y and the half-open range [X1, X2).</summary>
public readonly record struct EdgeSegment(int WindowIndex, int Y, int X1, int X2);

/// <summary>
/// Cuts each window's top edge where windows in front of it cover that line (design §6-4).
/// Windows are given front-most first.
/// </summary>
public static class TopEdgeClipper
{
    public const int MinSegmentPx = 24;

    public static IReadOnlyList<EdgeSegment> Clip(IReadOnlyList<RectI> windowsTopFirst)
    {
        var result = new List<EdgeSegment>();
        for (int i = 0; i < windowsTopFirst.Count; i++)
        {
            var w = windowsTopFirst[i];
            int y = w.Y;
            var pieces = new List<(int A, int B)> { (w.X, w.Right) };

            for (int j = 0; j < i && pieces.Count > 0; j++)
            {
                var f = windowsTopFirst[j];
                if (!(f.Y <= y && y < f.Bottom)) continue;
                pieces = Subtract(pieces, f.X, f.Right);
            }

            foreach (var (a, b) in pieces)
                if (b - a >= MinSegmentPx) result.Add(new EdgeSegment(i, y, a, b));
        }
        return result;
    }

    static List<(int A, int B)> Subtract(List<(int A, int B)> pieces, int cutA, int cutB)
    {
        var next = new List<(int A, int B)>(pieces.Count + 1);
        foreach (var (a, b) in pieces)
        {
            if (cutB <= a || cutA >= b) { next.Add((a, b)); continue; }
            if (cutA > a) next.Add((a, cutA));
            if (cutB < b) next.Add((cutB, b));
        }
        return next;
    }
}
