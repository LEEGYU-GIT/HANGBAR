using Godot;
using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>
/// Polls the cursor (GetCursorPos — no hooks) and turns each overlay's mouse passthrough
/// off only while the cursor is over something clickable (design §6-2).
/// </summary>
public partial class ClickThroughDriver : Node
{
    OverlayManager _overlays = null!;
    double _wait;
    readonly Dictionary<OverlayWindow, bool> _state = new();

    public bool Paused { get; set; }
    public PointI LastCursor { get; private set; }
    public event Action<OverlayWindow, bool>? PassthroughChanged;

    public void Init(OverlayManager overlays)
    {
        _overlays = overlays;
        _overlays.Rebuilt += () => _state.Clear();
    }

    public override void _Process(double delta)
    {
        if (Paused) return;
        _wait -= delta * 1000.0;
        if (_wait > 0) return;

        Win32.GetCursorPos(out var p);
        LastCursor = new PointI(p.X, p.Y);
        int next = ClickThroughPolicy.FarPollMs;

        foreach (var w in _overlays.Windows)
        {
            var decision = ClickThroughPolicy.Decide(LastCursor, HitRectsOf(w));
            next = Math.Min(next, decision.NextPollMs);
            if (_state.TryGetValue(w, out var current) && current == decision.Passthrough) continue;
            _state[w] = decision.Passthrough;
            // Godot's own MousePassthrough only works within this process; other apps need WS_EX_TRANSPARENT.
            Win32.ChangeExStyle(w.Hwnd,
                decision.Passthrough ? Win32.WS_EX_TRANSPARENT : 0,
                decision.Passthrough ? 0 : Win32.WS_EX_TRANSPARENT);
            PassthroughChanged?.Invoke(w, decision.Passthrough);
        }
        _wait = next;
    }

    /// <summary>Hit rectangles of everything clickable in a window, in screen pixels.</summary>
    static List<RectI> HitRectsOf(OverlayWindow w)
    {
        var list = new List<RectI>();
        foreach (var child in w.GetChildren())
        {
            if (child is StickFigure f)
            {
                var r = f.LocalHitRect;
                list.Add(new RectI(w.ScreenRect.X + (int)r.Position.X, w.ScreenRect.Y + (int)r.Position.Y, (int)r.Size.X, (int)r.Size.Y));
            }
        }
        return list;
    }
}
