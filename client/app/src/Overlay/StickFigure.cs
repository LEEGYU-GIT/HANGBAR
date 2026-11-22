using Godot;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>Spike-only stick figure: walks left and right along a floor, jumps when poked.</summary>
public partial class StickFigure : Node2D
{
    const float Speed = 60f;        // px per second
    const float Width = 28f, Height = 64f;

    public float FloorY { get; set; }
    public float MaxX { get; set; } = 800;
    float _dir = 1, _vy, _walk;

    /// <summary>Hit rectangle in this window's local pixels.</summary>
    public Rect2 LocalHitRect => new(Position.X - Width / 2, Position.Y - Height, Width, Height);

    public void Jump() { if (Position.Y >= FloorY - 0.5f) _vy = -420f; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var p = Position;
        p.X += _dir * Speed * dt;
        if (p.X > MaxX - Width) { p.X = MaxX - Width; _dir = -1; }
        if (p.X < Width) { p.X = Width; _dir = 1; }
        _vy += 1200f * dt;
        p.Y = Math.Min(p.Y + _vy * dt, FloorY);
        if (p.Y >= FloorY) _vy = 0;
        Position = p;
        _walk += dt * 8f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = new Color(0.12f, 0.12f, 0.14f);
        float swing = MathF.Sin(_walk) * 10f;
        DrawCircle(new Vector2(0, -52), 10, new Color(1, 0.86f, 0.4f));
        DrawArc(new Vector2(0, -52), 10, 0, MathF.Tau, 24, c, 2);
        DrawLine(new Vector2(0, -42), new Vector2(0, -20), c, 3);
        DrawLine(new Vector2(0, -36), new Vector2(-10, -26 + swing * 0.3f), c, 3);
        DrawLine(new Vector2(0, -36), new Vector2(10, -26 - swing * 0.3f), c, 3);
        DrawLine(new Vector2(0, -20), new Vector2(-6 + swing * 0.5f, 0), c, 3);
        DrawLine(new Vector2(0, -20), new Vector2(6 - swing * 0.5f, 0), c, 3);
    }
}
