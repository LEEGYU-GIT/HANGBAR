using Godot;
using Hangbar.Core;

namespace Hangbar.App.Spike;

/// <summary>Spike-only: draws terrain pieces (screen-pixel rects/segments) into one overlay window.</summary>
public partial class TerrainDebug : Node2D
{
    public RectI WindowRect { get; set; }
    public IReadOnlyList<RectI> Folders { get; set; } = Array.Empty<RectI>();
    public IReadOnlyList<RectI> Others { get; set; } = Array.Empty<RectI>();
    public IReadOnlyList<(int Y, int X1, int X2)> Edges { get; set; } = Array.Empty<(int, int, int)>();
    public IReadOnlyList<(int Y, int X1, int X2)> Floors { get; set; } = Array.Empty<(int, int, int)>();

    Rect2 Local(RectI r) => new(r.X - WindowRect.X, r.Y - WindowRect.Y, r.W, r.H);

    public override void _Draw()
    {
        foreach (var r in Folders) { DrawRect(Local(r), new Color(0.2f, 0.9f, 0.3f, 0.25f)); DrawRect(Local(r), new Color(0.2f, 0.9f, 0.3f), false, 2); }
        foreach (var r in Others) DrawRect(Local(r), new Color(0.7f, 0.7f, 0.7f, 0.8f), false, 1);
        foreach (var (y, x1, x2) in Edges) DrawLine(new Vector2(x1 - WindowRect.X, y - WindowRect.Y), new Vector2(x2 - WindowRect.X, y - WindowRect.Y), new Color(1, 0.85f, 0), 3);
        foreach (var (y, x1, x2) in Floors) DrawLine(new Vector2(x1 - WindowRect.X, y - WindowRect.Y), new Vector2(x2 - WindowRect.X, y - WindowRect.Y), new Color(0.2f, 0.5f, 1), 3);
    }
}
