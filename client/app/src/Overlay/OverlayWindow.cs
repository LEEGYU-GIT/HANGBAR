using Godot;
using Hangbar.App.Native;
using Hangbar.Core;

namespace Hangbar.App.Overlay;

/// <summary>Transparent, always-on-top, non-activating window covering one monitor (minus 1px).</summary>
public partial class OverlayWindow : Window
{
    public Hangbar.Core.Monitor Monitor { get; private set; } = null!;
    public nint Hwnd { get; private set; }
    public RectI ScreenRect { get; private set; }

    public static OverlayWindow Create(Hangbar.Core.Monitor monitor)
    {
        var rect = OverlayGeometry.OverlayRectFor(monitor);
        var w = new OverlayWindow
        {
            Monitor = monitor,
            ScreenRect = rect,
            Title = "HANGBAR overlay",
            Transparent = true,
            TransparentBg = true,
            Borderless = true,
            AlwaysOnTop = true,
            Unfocusable = true,
            Unresizable = true,
            Position = new Vector2I(rect.X, rect.Y),
            Size = new Vector2I(rect.W, rect.H),
        };
        return w;
    }

    public override void _Ready()
    {
        Hwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, GetWindowId());
        // No taskbar button, no Alt+Tab entry, never takes activation.
        Win32.ChangeExStyle(Hwnd, Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE, Win32.WS_EX_APPWINDOW);
        Win32.SetWindowPos(Hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
        GetTree().CreateTimer(1.0).Timeout += () =>
        {
            Win32.GetWindowRect(Hwnd, out var r);
            GD.Print($"[overlay] hwnd={Hwnd} visible={Visible} osVisible={Win32.IsWindowVisible(Hwnd)} " +
                     $"godotPos={Position} godotSize={Size} osRect={r.Left},{r.Top},{r.Right},{r.Bottom} " +
                     $"exStyle=0x{Win32.GetWindowLongPtr(Hwnd, Win32.GWL_EXSTYLE):X}");
        };
    }

    /// <summary>Converts a screen point (physical px) to this window's local coordinates.</summary>
    public Vector2 ToLocal(PointI p) => new(p.X - ScreenRect.X, p.Y - ScreenRect.Y);
}
