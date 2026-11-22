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

        Fullscreen = new FullscreenWatcher { Name = "Fullscreen" };
        Fullscreen.Init(Overlays, ClickThrough, () => Overlays.Windows.Select(w => w.Hwnd).Append(mainHwnd));
        Fullscreen.HiddenChanged += hidden => GD.Print($"[fullscreen] hidden={hidden}");
        AddChild(Fullscreen);

        var terrain = new Spike.TerrainSpike { Name = "Terrain" };
        terrain.Init(Overlays, () => Overlays.Windows.Select(w => w.Hwnd).Append(mainHwnd).ToHashSet());
        AddChild(terrain);

        _icons = new Terrain.DesktopIconReader();
        var iconTimer = new Godot.Timer { WaitTime = 2.0, Autostart = true };
        iconTimer.Timeout += ReadIcons;
        AddChild(iconTimer);
        ReadIcons();
    }

    Terrain.DesktopIconReader _icons = null!;
    string _lastIconLog = "";

    async void ReadIcons()
    {
        var r = await _icons.ReadAsync();
        foreach (var w in Overlays.Windows)
        {
            if (w.GetNodeOrNull<Spike.TerrainDebug>("TerrainDebug") is not { } dbg) continue;
            dbg.Folders = r.FolderRects;
            dbg.Others = r.OtherRects;
            dbg.QueueRedraw();
        }
        // Counts only, never names (design section 3).
        string log = r.Ok ? $"[icons] ok folders={r.FolderRects.Count} others={r.OtherRects.Count} first={(r.FolderRects.Count > 0 ? r.FolderRects[0] : r.OtherRects.FirstOrDefault())}"
                          : $"[icons] failed {r.Error}";
        if (log != _lastIconLog) GD.Print(log);
        _lastIconLog = log;
    }

    public override void _ExitTree() => _icons?.Dispose();

    public FullscreenWatcher Fullscreen { get; private set; } = null!;

    public ClickThroughDriver ClickThrough { get; private set; } = null!;
}
