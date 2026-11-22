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
            w.AddChild(new TerrainDebug { Name = "TerrainDebug", WindowRect = w.ScreenRect });
            var figure = new StickFigure
            {
                Name = "Figure",
                FloorY = w.Size.Y - 80,
                MaxX = w.Size.X,
                Position = new Vector2(w.Size.X / 3f, w.Size.Y - 80),
            };
            w.AddChild(figure);
            // Spike-only: extra walkers for resource measurement (HANGBAR_FIGURES=3 → 3 walkers).
            if (int.TryParse(System.Environment.GetEnvironmentVariable("HANGBAR_FIGURES"), out int n))
                for (int i = 1; i < n; i++)
                    w.AddChild(new StickFigure { Name = $"Extra{i}", FloorY = figure.FloorY, MaxX = w.Size.X, Position = new Vector2(w.Size.X * (0.15f + 0.2f * i), figure.FloorY) });
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
