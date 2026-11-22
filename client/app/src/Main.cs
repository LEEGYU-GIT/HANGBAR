using Godot;
using Hangbar.App.Overlay;

namespace Hangbar.App;

/// <summary>Root node. The main window itself stays a 1x1 transparent window; overlays do the drawing.</summary>
public partial class Main : Node
{
    public OverlayManager Overlays { get; private set; } = null!;

    public override void _Ready()
    {
        var main = GetWindow();
        main.Size = new Vector2I(1, 1);
        main.Position = new Vector2I(0, 0);
        main.MousePassthrough = true;
        var mainHwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, main.GetWindowId());
        Native.Win32.ChangeExStyle(mainHwnd, Native.Win32.WS_EX_TOOLWINDOW | Native.Win32.WS_EX_NOACTIVATE, Native.Win32.WS_EX_APPWINDOW);

        Overlays = new OverlayManager { Name = "Overlays" };
        AddChild(Overlays);

        ClickThrough = new ClickThroughDriver { Name = "ClickThrough" };
        ClickThrough.Init(Overlays);
        ClickThrough.PassthroughChanged += (w, on) =>
            GD.Print($"[click] {w.Monitor.Id} passthrough={on} cursor={ClickThrough.LastCursor} " +
                     $"exStyle=0x{Native.Win32.GetWindowLongPtr(w.Hwnd, Native.Win32.GWL_EXSTYLE):X}");
        AddChild(ClickThrough);
    }

    public ClickThroughDriver ClickThrough { get; private set; } = null!;
}
