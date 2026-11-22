using Godot;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>Spike-only stick figure: walks left and right along a floor, jumps when poked.</summary>
public partial class StickFigure : Node2D
{
    const float Speed = 60f;        // px per second
    const float Width = 28f, Height = 64f;

    public float FloorY { get; set; }
    public bool Walk { get; set; } = true;
    public float MaxX { get; set; } = 800;
    float _dir = 1, _vy, _walk;

    /// <summary>Hit rectangle in this window's local pixels.</summary>
    public Rect2 LocalHitRect => new(Position.X - Width / 2, Position.Y - Height, Width, Height);

    public void Jump() { if (Position.Y >= FloorY - 0.5f) _vy = -420f; }

    public event Action? Poked;

    Label? _bubble;
    double _bubbleLeft;

    /// <summary>Shows a speech bubble above the head for 4 seconds.</summary>
    public void Say(string text)
    {
        if (_bubble is null)
        {
            _bubble = new Label { Position = new Vector2(-120, -110), Size = new Vector2(240, 40), HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _bubble.AddThemeFontOverride("font", new SystemFont { FontNames = new[] { "Malgun Gothic", "Segoe UI" } });
            _bubble.AddThemeColorOverride("font_color", new Color(0.1f, 0.1f, 0.1f));
            _bubble.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.95f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 });
            AddChild(_bubble);
        }
        _bubble.Text = text;
        _bubble.Visible = true;
        _bubbleLeft = 4.0;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb && LocalHitRect.HasPoint(mb.Position))
        {
            Jump();
            Poked?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_bubble is { Visible: true } && (_bubbleLeft -= delta) <= 0) _bubble.Visible = false;
        var p = Position;
        if (Walk)
        {
            p.X += _dir * Speed * dt;
            if (p.X > MaxX - Width) { p.X = MaxX - Width; _dir = -1; }
            if (p.X < Width) { p.X = Width; _dir = 1; }
        }
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
