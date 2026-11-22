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

        if (System.Environment.GetEnvironmentVariable("HANGBAR_CHAT") != "0")
        {
            Chat = new Spike.ChatWindow { Name = "Chat", Position = new Vector2I(80, 120) };
            AddChild(Chat);
            Chat.Append("시스템", "한글 입력 시험: 안녕하세요 반가워요 ㅋㅋ");
            Chat.Submitted += text => { Chat.Append("나", text); GD.Print($"[chat] submitted len={text.Length}"); };
            Chat.SignInRequested += () => _ = SignInAsync(selfTest: false);
        }

        if (System.Environment.GetEnvironmentVariable("HANGBAR_AUTH_SELFTEST") == "1") _ = SignInAsync(selfTest: true);

        // Protocol activation (MSIX windows.protocol) passes the URI as a command-line argument.
        var join = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs())
            .FirstOrDefault(a => a.StartsWith("hangbar://join/", StringComparison.OrdinalIgnoreCase));
        if (join is not null)
        {
            var code = join["hangbar://join/".Length..].Trim('/');
            GD.Print($"[link] join code={code}");
            if (Overlays.Hud is { } hud) hud.Extra = $"\njoin {code}";
        }

        var room = new Spike.RoomSpike { Name = "Room" };
        room.Init(Overlays, Chat);
        AddChild(room);
        var cfg = Net.HangbarConfig.Load(ProjectSettings.GlobalizePath("res://"));
        if (cfg.RelayUrl is { Length: > 0 } relayUrl)
        {
            var relay = new Uri(relayUrl.EndsWith('/') ? relayUrl : relayUrl + "/");
            SignedIn += s => room.Connect(relay, s.AccessToken);
            // Spike-only shortcut for testing the relay without a Supabase account.
            if (System.Environment.GetEnvironmentVariable("HANGBAR_DEV_TOKEN") is { Length: > 0 } devToken) room.Connect(relay, devToken);
        }

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

    public Net.Session? Session { get; private set; }
    public event Action<Net.Session>? SignedIn;

    /// <summary>Self-test (no Supabase account): listener + callback parsing only; the token exchange is expected to fail.</summary>
    async Task SignInAsync(bool selfTest)
    {
        var config = Net.HangbarConfig.Load(ProjectSettings.GlobalizePath("res://"));
        if (!selfTest && !config.CanSignIn)
        {
            Chat?.SetStatus("Supabase 설정 없음 (hangbar.local.json)");
            GD.Print("[auth] no supabase config");
            return;
        }
        var auth = selfTest
            ? new Net.AuthLoopback("http://127.0.0.1:9", "selftest", url => GD.Print($"[auth] would open browser (url length {url.Length})"))
            : new Net.AuthLoopback(config.SupabaseUrl!, config.SupabaseKey!, url => OS.ShellOpen(url));
        auth.Log += line => CallDeferred(nameof(LogLine), line);
        Chat?.SetStatus("브라우저에서 로그인 중...");
        try
        {
            var s = await auth.SignInAsync(CancellationToken.None);
            if (s is null) { Chat?.SetStatus("로그인 실패"); return; }
            Session = s;
            Chat?.SetStatus($"로그인됨 {s.UserId[..8]}");
            SignedIn?.Invoke(s);
        }
        catch (Exception e)
        {
            GD.Print($"[auth] error {e.GetType().Name}");
            Chat?.SetStatus("로그인 오류");
        }
    }

    void LogLine(string line) => GD.Print(line);

    public FullscreenWatcher Fullscreen { get; private set; } = null!;
    public Spike.ChatWindow? Chat { get; private set; }

    public ClickThroughDriver ClickThrough { get; private set; } = null!;
}
