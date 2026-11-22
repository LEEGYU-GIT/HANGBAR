using Godot;
using Hangbar.App.Native;
using Hangbar.App.Overlay;
using Hangbar.App.Terrain;
using Hangbar.Core;

namespace Hangbar.App.Spike;

/// <summary>
/// Spike for risk 7: draws clipped window top edges and taskbar floors, and keeps one "rider"
/// figure on a window's top edge — it moves with the window and falls when the window goes away.
/// Window list every 1s; the ridden window every 200ms (design §6-4).
/// </summary>
public partial class TerrainSpike : Node
{
    OverlayManager _overlays = null!;
    Func<IReadOnlySet<nint>> _own = null!;
    double _slow = 1.0, _fast;
    bool _enabled = System.Environment.GetEnvironmentVariable("HANGBAR_TERRAIN_POLL") != "0";

    StickFigure? _rider;
    OverlayWindow? _riderWindow;
    nint _ridden;
    int _offsetX;
    readonly Dictionary<string, int> _floorY = new();
    Task<List<TaskbarReader.Taskbar>>? _taskbarRead;
    List<TaskbarReader.Taskbar> _bars = new();

    public void Init(OverlayManager overlays, Func<IReadOnlySet<nint>> own)
    {
        _overlays = overlays;
        _own = own;
        _overlays.Rebuilt += () => { _rider = null; _riderWindow = null; _ridden = 0; };
    }

    public override void _Process(double delta)
    {
        if (!_enabled) return;
        _slow += delta;
        _fast += delta;
        if (_slow >= 1.0) { _slow = 0; ReadAll(); }
        if (_fast >= 0.2) { _fast = 0; FollowRidden(); }
    }

    void ReadAll()
    {
        var layout = new MonitorLayout(_overlays.Windows.Select(w => w.Monitor).ToList());
        var windows = WindowEdgeReader.Read(_own());
        var segments = TopEdgeClipper.Clip(windows.Select(w => w.Rect).ToList());

        // SHAppBarMessage waits on explorer; never call it on the main thread (review I7).
        // A hung explorer only leaves the last known floors in place.
        if (_taskbarRead is { IsCompleted: true } done)
        {
            if (done.IsCompletedSuccessfully) _bars = done.Result;
            _taskbarRead = null;
        }
        _taskbarRead ??= Task.Run(() => TaskbarReader.Read(layout));

        _floorY.Clear();
        foreach (var m in layout.Monitors)
        {
            var bar = _bars.FirstOrDefault(b => b.MonitorId == m.Id);
            _floorY[m.Id] = TaskbarFloor.FloorY(m, bar?.Rect, bar?.Edge ?? TaskbarEdge.Bottom, bar?.AutoHide ?? false);
        }

        foreach (var w in _overlays.Windows)
        {
            int floor = _floorY[w.Monitor.Id];
            if (w.GetNodeOrNull<TerrainDebug>("TerrainDebug") is { } dbg)
            {
                dbg.Edges = segments.Select(s => (s.Y, s.X1, s.X2)).ToList();
                dbg.Floors = new[] { (floor, w.Monitor.Bounds.X, w.Monitor.Bounds.Right) };
                dbg.QueueRedraw();
            }
            if (w.GetNodeOrNull<StickFigure>("Figure") is { } walker) walker.FloorY = floor - w.ScreenRect.Y;
        }

        if (_ridden == 0) Mount(windows, segments);
    }

    void Mount(List<(nint Hwnd, RectI Rect)> windows, IReadOnlyList<EdgeSegment> segments)
    {
        foreach (var s in segments)
        {
            var (hwnd, rect) = windows[s.WindowIndex];
            var w = _overlays.Windows.FirstOrDefault(o => o.ScreenRect.Contains(new PointI((s.X1 + s.X2) / 2, s.Y)));
            if (w is null || s.Y - w.ScreenRect.Y < 80) continue; // need room for the figure above the edge
            _ridden = hwnd;
            _offsetX = (s.X1 + s.X2) / 2 - rect.X;
            _riderWindow = w;
            _rider = new StickFigure { Name = "Rider", Walk = false, MaxX = w.Size.X };
            _rider.Position = new Vector2(rect.X + _offsetX - w.ScreenRect.X, s.Y - w.ScreenRect.Y);
            _rider.FloorY = _rider.Position.Y;
            w.AddChild(_rider);
            GD.Print($"[terrain] rider mounted on a window edge at y={s.Y} (windows={windows.Count} segments={segments.Count})");
            return;
        }
    }

    void FollowRidden()
    {
        if (_ridden == 0 || _rider is null || _riderWindow is null) return;
        var w = _riderWindow;
        if (!WindowEdgeReader.IsStandable(_ridden) || !Win32.TryFrameBounds(_ridden, out var r))
        {
            _rider.FloorY = _floorY.GetValueOrDefault(w.Monitor.Id, w.ScreenRect.Bottom) - w.ScreenRect.Y;
            GD.Print("[terrain] ridden window gone or minimized: rider falls");
            _ridden = 0;
            _rider.Name = "Fallen";
            _rider = null;
            return;
        }
        var rect = DisplayInfo.ToRect(r);
        _rider.Position = new Vector2(rect.X + _offsetX - w.ScreenRect.X, _rider.Position.Y);
        _rider.FloorY = rect.Y - w.ScreenRect.Y;
    }
}
