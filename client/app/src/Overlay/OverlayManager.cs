using Godot;
using Hangbar.App.Native;
using Hangbar.App.Spike;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>Owns one overlay window per monitor and rebuilds them when the layout changes.</summary>
public partial class OverlayManager : Node
{
    readonly List<OverlayWindow> _windows = new();
    MonitorLayout _layout = new(Array.Empty<Hangbar.Core.Monitor>());
    double _since;

    public IReadOnlyList<OverlayWindow> Windows => _windows;
    public SpikeHud? Hud { get; private set; }
    public event Action? Rebuilt;

    public override void _Ready() => Rebuild(DisplayInfo.Read());

    public override void _Process(double delta)
    {
        _since += delta;
        if (_since < 2.0) return;
        _since = 0;
        var now = DisplayInfo.Read();
        if (!MonitorLayout.Diff(_layout, now).IsEmpty) Rebuild(now);
    }

    public void Rebuild(MonitorLayout layout)
    {
        foreach (var w in _windows) w.QueueFree();
        _windows.Clear();
        Hud = null;
        _layout = layout;

        foreach (var m in layout.Monitors)
        {
            var w = OverlayWindow.Create(m);
            var figure = new StickFigure
            {
                Name = "Figure",
                FloorY = w.Size.Y - 80,
                MaxX = w.Size.X,
                Position = new Vector2(w.Size.X / 3f, w.Size.Y - 80),
            };
            w.AddChild(figure);
            if (m.Primary)
            {
                Hud = new SpikeHud { Position = new Vector2(w.Size.X - 420, w.Size.Y - 120) };
                w.AddChild(Hud);
            }
            AddChild(w);
            _windows.Add(w);
        }
        GD.Print($"[overlay] rebuilt: {layout.Monitors.Count} monitor(s): " +
                 string.Join(", ", layout.Monitors.Select(m => $"{m.Bounds.W}x{m.Bounds.H}@{m.Bounds.X},{m.Bounds.Y} x{m.Scale:0.##}{(m.Primary ? " primary" : "")}")));
        Rebuilt?.Invoke();
    }

    public void SetAllVisible(bool visible)
    {
        foreach (var w in _windows) w.Visible = visible;
    }
}
