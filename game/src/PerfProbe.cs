using System.Diagnostics;
using System.Text.Json;
using Godot;

namespace Inpc.Game;

/// <summary>
/// F3 HUD (fps, frame time, triangles, draw calls, chunks) and the benchmark mode (--bench=SECONDS --out=FILE):
/// the camera flies a fixed path, eye height around the map for the first half and max(20 m, highest ground + 5 m) for the second half,
/// the first 2 s are discarded, then frame times are written as JSON and the game quits.
/// </summary>
public partial class PerfProbe : CanvasLayer
{
    const double Warmup = 2.0;

    WorldRoot _world = null!;
    Label _hud = null!;
    double _benchSeconds;
    string? _outPath;
    Camera3D? _benchCam;
    double _t;
    readonly List<double> _frameMs = new();
    readonly Stopwatch _clock = new();
    long _tris, _draws;

    public void Init(WorldRoot world, double benchSeconds, string? outPath)
    {
        _world = world;
        _benchSeconds = benchSeconds;
        _outPath = outPath;
    }

    public override void _Ready()
    {
        _hud = new Label { Position = new Vector2(12, 8), Visible = _benchSeconds > 0 };
        _hud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        AddChild(_hud);
        if (_benchSeconds > 0)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            _world.Player.InputEnabled = false;
            _benchCam = new Camera3D { Fov = 70, Far = 300 };
            _world.AddChild(_benchCam);
            _benchCam.MakeCurrent();
            _clock.Start();
        }
    }

    /// <summary>
    /// --shots=DIR: saves overview.png (30 m up, oblique), ground.png (behind the character at eye height),
    /// top.png (straight down, orthographic, to lay next to the photo) and photo.png (the prepared 192×192 photo), then quits.
    /// File names never include the photo's name.
    /// </summary>
    public async void TakeShots(string dir, Hangbar.Core.World.PixelImage prepared)
    {
        System.IO.Directory.CreateDirectory(dir);
        _world.Player.InputEnabled = false;
        var cam = new Camera3D { Fov = 60, Far = 400 };
        _world.AddChild(cam);
        float size = _world.Map.Size * Hangbar.Core.World.ChunkMesher.BlockSize, half = size / 2;
        var center = new Vector3(half, 0, half);
        var feet = _world.Player.GlobalPosition;

        async Task Shot(string name)
        {
            for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name));
        }

        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        cam.MakeCurrent();
        cam.LookAtFromPosition(center + new Vector3(-half * 0.9f, 30, half * 1.1f), center, Vector3.Up);
        await Shot("overview.png");
        _world.Player.Camera.MakeCurrent();
        await Shot("ground.png");
        cam.MakeCurrent();
        cam.Projection = Camera3D.ProjectionType.Orthogonal;
        cam.Size = size;
        cam.LookAtFromPosition(center + new Vector3(0, 60, 0.001f), center, new Vector3(0, 0, -1));
        await Shot("top.png");

        var img = Image.CreateEmpty(prepared.Width, prepared.Height, false, Image.Format.Rgba8);
        for (int y = 0; y < prepared.Height; y++)
            for (int x = 0; x < prepared.Width; x++)
            {
                var (r, g, b, a) = Hangbar.Core.World.PixelImage.Unpack(prepared[x, y]);
                img.SetPixel(x, y, Color.Color8((byte)r, (byte)g, (byte)b, (byte)a));
            }
        img.SavePng(System.IO.Path.Combine(dir, "photo.png"));
        GD.Print($"[shots] saved 4 images ({feet.Y:0.00} m spawn height)");
        GetTree().Quit();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.F3 }) _hud.Visible = !_hud.Visible;
    }

    public override void _Process(double delta)
    {
        long draws = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
        long tris = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
        if (_hud.Visible)
            _hud.Text = $"{Engine.GetFramesPerSecond():0} fps  {delta * 1000:0.0} ms\ntris {tris:N0}  draws {draws}  chunks {_world.ChunkNodes}";
        if (_benchCam is null) return;

        double ms = _clock.Elapsed.TotalMilliseconds;
        _clock.Restart();
        _t += ms / 1000.0;
        PlaceBenchCamera(_t / _benchSeconds);
        if (_t > Warmup)
        {
            _frameMs.Add(ms);
            _tris = Math.Max(_tris, tris);
            _draws = Math.Max(_draws, draws);
        }
        if (_t >= _benchSeconds) Finish();
    }

    void PlaceBenchCamera(double f)
    {
        float size = _world.Map.Size * Hangbar.Core.World.ChunkMesher.BlockSize, half = size / 2;
        var center = new Vector3(half, 0, half);
        double a = f * Math.PI * 4; // two laps
        if (f < 0.5)
        {
            var p = center + new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a)) * (half * 0.75f);
            p.Y = GroundAt(p) + 1.6f;
            var ahead = center + new Vector3((float)Math.Cos(a + 0.2), 0, (float)Math.Sin(a + 0.2)) * (half * 0.75f);
            ahead.Y = p.Y;
            _benchCam!.LookAtFromPosition(p, ahead, Vector3.Up);
        }
        else
        {
            var p = center + new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a)) * (half * 0.6f);
            p.Y = HighPath();
            _benchCam!.LookAtFromPosition(p, center, Vector3.Up);
        }
    }

    /// <summary>Height of the upper lap: 20 m, or 5 m over the highest ground when the world is taller (v2 mountains).</summary>
    float HighPath() => Math.Max(20f, _world.Map.Height.Max() * Hangbar.Core.World.ChunkMesher.BlockSize + 5f);

    float GroundAt(Vector3 p)
    {
        var m = _world.Map;
        int x = Math.Clamp((int)(p.X / Hangbar.Core.World.ChunkMesher.BlockSize), 0, m.Size - 1);
        int z = Math.Clamp((int)(p.Z / Hangbar.Core.World.ChunkMesher.BlockSize), 0, m.Size - 1);
        int i = m.Index(x, z);
        return Math.Max(m.Height[i], m.Water[i] ? m.WaterLevel : 0) * Hangbar.Core.World.ChunkMesher.BlockSize;
    }

    void Finish()
    {
        var sorted = _frameMs.OrderByDescending(v => v).ToList();
        int worst = Math.Max(1, sorted.Count / 100);
        double avgMs = _frameMs.Average();
        var size = GetViewport().GetVisibleRect().Size;
        var result = new Dictionary<string, object>
        {
            ["avgFps"] = Math.Round(1000.0 / avgMs, 1),
            ["p1LowFps"] = Math.Round(1000.0 / sorted.Take(worst).Average(), 1),
            ["minFps"] = Math.Round(1000.0 / sorted[0], 1),
            ["frameMsP95"] = Math.Round(sorted[Math.Min(sorted.Count - 1, sorted.Count / 20)], 2),
            ["frames"] = _frameMs.Count,
            ["tris"] = _tris,
            ["drawCalls"] = _draws,
            ["chunks"] = _world.ChunkNodes,
            ["resolution"] = $"{(int)size.X}x{(int)size.Y}",
            ["quality"] = _world.Quality,
            ["gpu"] = RenderingServer.GetVideoAdapterName(),
        };
        string json = JsonSerializer.Serialize(result);
        GD.Print($"[bench] {json}");
        if (_outPath is not null) System.IO.File.WriteAllText(_outPath, json);
        _benchCam = null;
        GetTree().Quit();
    }
}
