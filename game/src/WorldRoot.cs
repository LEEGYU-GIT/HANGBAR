using System.Diagnostics;
using Godot;
using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Inpc.Game;

/// <summary>
/// Root of the 3D world (design §6-3): photo → file map → voxel world → one mesh and collider per chunk.
/// User args after "--": --photo=PATH, --scene=beach|pool|house|mountain (v2 generator; without it the G0 world),
/// --quality=low|medium, --bench=SECONDS, --shots=DIR, --out=FILE.
/// </summary>
public partial class WorldRoot : Node3D
{
    public VoxelWorld World { get; private set; } = null!;
    public FileMap Map { get; private set; } = null!;
    public Vector3 SpawnPoint { get; private set; }
    public Dictionary<string, string> Args { get; } = new();
    public int ChunkNodes { get; private set; }
    public string Quality => Args.GetValueOrDefault("quality", "medium");

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--") && a.Contains('=')) Args[a[2..a.IndexOf('=')]] = a[(a.IndexOf('=') + 1)..];
        Style.Soft = Args.GetValueOrDefault("style") == "soft"; // SPIKE: style test

        var sw = Stopwatch.StartNew();
        PixelImage? photo = null;
        if (Args.TryGetValue("photo", out var path))
        {
            photo = PhotoLoader.Load(path);
            if (photo is null) GD.Print("[world] photo unreadable, using sample");
        }
        photo ??= SamplePhotos.Meadow();
        _photo = photo;
        float bs = ChunkMesher.BlockSize;

        if (Args.TryGetValue("scene", out var sceneName))
        {
            // v2: a scene variant built from the photo's colours, mood and seed (v2 design §5)
            if (Scenes.FromName(sceneName) is not { } scene)
            {
                GD.Print("[world] unknown scene; use beach, pool, house or mountain");
                GetTree().Quit(2);
                return;
            }
            var prepared = ImageFit.Prepare(photo, SceneContext.Size);
            var g = WorldGenerator.Generate(scene, Palette.Extract(prepared), MoodReader.Read(prepared), prepared, WorldSeed.Of(prepared),
                Horizon.Estimate(prepared));
            _generated = g;
            World = g.World;
            Map = g.Surface;
            long gen2 = sw.ElapsedMilliseconds;
            sw.Restart();
            var (tris2, chunks2) = WorldView.Build(this, World);
            ChunkNodes = chunks2;
            int props = PropsView.Build(this, World, g.Props);
            long mesh2 = sw.ElapsedMilliseconds;
            SpawnPoint = new Vector3((g.Spawn.X + 0.5f) * bs, g.Spawn.Y * bs + 0.05f, (g.Spawn.Z + 0.5f) * bs);
            SetUpEnvironment();
            var m = g.Mural;
            GD.Print($"[world] scene={g.Scene.ToString().ToLowerInvariant()} seed={g.Seed:x16} tris={tris2} props={props} " +
                     $"mural=({m.Origin.X},{m.Origin.Y},{m.Origin.Z} {m.Facing.ToString().ToLowerInvariant()} {m.Width}x{m.Height}) " +
                     $"landmark={g.Landmark.Name} chunks={ChunkNodes} gen={gen2} mesh={mesh2}");
            OnWorldReady();
            return;
        }

        Map = FileMapGenerator.Generate(photo, new MapGenSettings());
        World = VoxelWorld.FromFileMap(Map);
        long genMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var (tris, chunks) = WorldView.Build(this, World);
        ChunkNodes = chunks;
        long meshMs = sw.ElapsedMilliseconds;

        int c = Map.Size / 2;
        SpawnPoint = new Vector3((c + 0.5f) * bs, Map.StandHeight(c, c) * bs + 0.5f, (c + 0.5f) * bs);

        SetUpEnvironment();
        GD.Print($"[world] photo={photo.Width}x{photo.Height} map={Map.Size} seed={Map.Seed:x16} chunks={ChunkNodes} tris={tris} gen={genMs} mesh={meshMs}");
        OnWorldReady();
    }

    GeneratedWorld? _generated;

    public Player Player { get; private set; } = null!;
    PixelImage _photo = null!;

    void OnWorldReady()
    {
        AddEdgeWalls();
        Player = new Player { Position = SpawnPoint };
        AddChild(Player);
        if (_generated is { } g) Player.SetView(g.FacingDegrees, g.PitchDegrees);
        if (Args.TryGetValue("walktest", out var secs) && float.TryParse(secs, out var s)) _ = WalkTest(s);

        double bench = Args.TryGetValue("bench", out var b) && double.TryParse(b, out var bs) ? bs : 0;
        var probe = new PerfProbe();
        probe.Init(this, bench, Args.GetValueOrDefault("out"));
        AddChild(probe);
        if (Args.TryGetValue("shots", out var dir))
        {
            if (ShotOutput.Problem(dir, Args.GetValueOrDefault("photo")) is { } problem)
            {
                GD.Print($"[shots] not saved: {problem}");
                GetTree().Quit(1);
            }
            else probe.TakeShots(dir, ImageFit.Prepare(_photo, Map.Size));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player is not null && Player.GlobalPosition.Y < -5f)
        {
            Player.GlobalPosition = SpawnPoint;
            Player.Velocity = Vector3.Zero;
        }
    }

    /// <summary>Invisible walls just outside the four map edges, as high as the world (at least 20 m).</summary>
    void AddEdgeWalls()
    {
        float size = Map.Size * ChunkMesher.BlockSize, h = Math.Max(20f, World.SizeY * ChunkMesher.BlockSize), t = 1f;
        foreach (var (pos, ext) in new[]
        {
            (new Vector3(size / 2, h / 2, -t / 2), new Vector3(size + 2 * t, h, t)),
            (new Vector3(size / 2, h / 2, size + t / 2), new Vector3(size + 2 * t, h, t)),
            (new Vector3(-t / 2, h / 2, size / 2), new Vector3(t, h, size)),
            (new Vector3(size + t / 2, h / 2, size / 2), new Vector3(t, h, size)),
        })
        {
            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = ext } });
            AddChild(body);
        }
    }

    /// <summary>
    /// Dev check for the step-up rule (plan Review Focus 4): walks toward the map's highest column for the given seconds
    /// and prints how far and how high the player got. A walker stuck on 0.25 m ledges barely moves.
    /// </summary>
    async Task WalkTest(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        int best = 0;
        for (int i = 1; i < Map.Height.Length; i++) if (!Map.Water[i] && Map.Height[i] > Map.Height[best]) best = i;
        var target = new Vector3((best % Map.Size + 0.5f) * ChunkMesher.BlockSize, 0, (best / Map.Size + 0.5f) * ChunkMesher.BlockSize);
        var start = Player.GlobalPosition;
        Player.AutoWalk = (target - start) with { Y = 0 };
        for (float t = 0; t < seconds; t += 0.5f)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            var p = Player.GlobalPosition;
            int gx = (int)(p.X / ChunkMesher.BlockSize), gz = (int)(p.Z / ChunkMesher.BlockSize);
            int ground = gx >= 0 && gz >= 0 && gx < Map.Size && gz < Map.Size ? Map.Height[Map.Index(gx, gz)] : -1;
            GD.Print($"[walktest] t={t + 0.5f:0.0} pos=({p.X:0.00},{p.Y:0.00},{p.Z:0.00}) groundTop={ground * ChunkMesher.BlockSize:0.00} floor={Player.IsOnFloor()}");
        }
        var end = Player.GlobalPosition;
        GD.Print($"[walktest] moved={(end - start).Slide(Vector3.Up).Length():0.00}m expected~{4.5f * seconds:0.0}m rise={end.Y - start.Y:0.00}m endFloor={Player.IsOnFloor()}");
        Player.AutoWalk = null;
        GetTree().Quit();
    }

    void SetUpEnvironment()
    {
        bool medium = Quality != "low";
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.3f, 0.5f, 0.78f),
            SkyHorizonColor = new Color(0.82f, 0.86f, 0.9f),
            GroundHorizonColor = new Color(0.82f, 0.86f, 0.9f),   // same as the sky horizon: no dark band past the map edge
            GroundBottomColor = new Color(0.6f, 0.66f, 0.72f),
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.1f,
            GlowEnabled = true,
            GlowIntensity = 0.3f,
            SsaoEnabled = false,
            SdfgiEnabled = false,
            VolumetricFogEnabled = false,
        };
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -35, 0),
            LightEnergy = 1.3f,
            ShadowEnabled = medium,
            DirectionalShadowMaxDistance = 60,
        };
        if (Style.Soft)
        {
            // SPIKE: warm, saturated sky; distance fades into a soft haze; warmer sun, softer shadows
            var haze = new Color(0.78f, 0.86f, 0.96f);
            sky.SkyTopColor = new Color(0.22f, 0.52f, 0.95f);
            sky.SkyHorizonColor = haze;
            sky.GroundHorizonColor = haze;
            sky.GroundBottomColor = haze;
            env.AmbientLightEnergy = 0.9f;
            env.TonemapExposure = 1.0f;
            env.FogEnabled = true;
            env.FogLightColor = haze;
            env.FogDensity = 0.005f;
            env.FogAerialPerspective = 0.35f;
            env.FogSkyAffect = 0.0f;
            env.AdjustmentEnabled = true;
            env.AdjustmentSaturation = 1.3f;
            sun.LightColor = new Color(1f, 0.94f, 0.84f);
            sun.LightEnergy = 1.25f;
            sun.ShadowBlur = 2.5f;
            sun.ShadowOpacity = 0.75f;
        }
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(sun);
        GetViewport().ScreenSpaceAA = medium ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
    }
}
