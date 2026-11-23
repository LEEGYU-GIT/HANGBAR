using System.Diagnostics;
using Godot;
using Hangbar.Core.World;

namespace Inpc.Game;

/// <summary>
/// Root of the 3D world (design §6-3): photo → file map → voxel world → one mesh and collider per chunk.
/// User args after "--": --photo=PATH, --quality=low|medium, --bench=SECONDS, --shots=DIR, --out=FILE.
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

        var sw = Stopwatch.StartNew();
        PixelImage? photo = null;
        if (Args.TryGetValue("photo", out var path))
        {
            photo = PhotoLoader.Load(path);
            if (photo is null) GD.Print("[world] photo unreadable, using sample");
        }
        photo ??= SamplePhotos.Meadow();
        _photo = photo;

        Map = FileMapGenerator.Generate(photo, new MapGenSettings());
        World = VoxelWorld.FromFileMap(Map);
        long genMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int tris = BuildChunks();
        long meshMs = sw.ElapsedMilliseconds;

        int c = Map.Size / 2;
        float bs = ChunkMesher.BlockSize;
        SpawnPoint = new Vector3((c + 0.5f) * bs, Math.Max(Map.Height[Map.Index(c, c)], Map.WaterLevel) * bs + 0.5f, (c + 0.5f) * bs);

        SetUpEnvironment();
        GD.Print($"[world] photo={photo.Width}x{photo.Height} map={Map.Size} seed={Map.Seed:x16} chunks={ChunkNodes} tris={tris} gen={genMs} mesh={meshMs}");
        OnWorldReady();
    }

    public Player Player { get; private set; } = null!;
    PixelImage _photo = null!;

    void OnWorldReady()
    {
        AddEdgeWalls();
        Player = new Player { Position = SpawnPoint };
        AddChild(Player);
        if (Args.TryGetValue("walktest", out var secs) && float.TryParse(secs, out var s)) _ = WalkTest(s);

        double bench = Args.TryGetValue("bench", out var b) && double.TryParse(b, out var bs) ? bs : 0;
        var probe = new PerfProbe();
        probe.Init(this, bench, Args.GetValueOrDefault("out"));
        AddChild(probe);
        if (Args.TryGetValue("shots", out var dir)) probe.TakeShots(dir, ImageFit.Prepare(_photo, Map.Size));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player is not null && Player.GlobalPosition.Y < -5f)
        {
            Player.GlobalPosition = SpawnPoint;
            Player.Velocity = Vector3.Zero;
        }
    }

    /// <summary>Invisible walls 20 m high just outside the four map edges.</summary>
    void AddEdgeWalls()
    {
        float size = Map.Size * ChunkMesher.BlockSize, h = 20f, t = 1f;
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

    int BuildChunks()
    {
        // Block colors come straight from the photo, so they are sRGB, not linear.
        var solidMat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f };
        var waterMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.1f,
        };
        int tris = 0;
        for (int cy = 0; cy < World.ChunksY; cy++)
            for (int cz = 0; cz < World.ChunksZ; cz++)
                for (int cx = 0; cx < World.ChunksX; cx++)
                {
                    var (solid, water) = ChunkMesher.Build(World, cx, cy, cz);
                    if (solid.TriangleCount > 0)
                    {
                        var mi = new MeshInstance3D { Mesh = ToMesh(solid), MaterialOverride = solidMat, Name = $"C{cx}_{cy}_{cz}" };
                        var body = new StaticBody3D();
                        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = Faces(solid), BackfaceCollision = true } });
                        mi.AddChild(body);
                        AddChild(mi);
                        ChunkNodes++;
                        tris += solid.TriangleCount;
                    }
                    if (water.TriangleCount > 0)
                    {
                        AddChild(new MeshInstance3D
                        {
                            Mesh = ToMesh(water), MaterialOverride = waterMat, Name = $"W{cx}_{cy}_{cz}",
                            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                        });
                        tris += water.TriangleCount;
                    }
                }
        return tris;
    }

    static ArrayMesh ToMesh(MeshData m)
    {
        int n = m.VertexCount;
        var pos = new Vector3[n];
        var nrm = new Vector3[n];
        var col = new Color[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
            nrm[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
            col[i] = new Color(m.Colors[i * 4], m.Colors[i * 4 + 1], m.Colors[i * 4 + 2], m.Colors[i * 4 + 3]);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = pos;
        arrays[(int)Mesh.ArrayType.Normal] = nrm;
        arrays[(int)Mesh.ArrayType.Color] = col;
        arrays[(int)Mesh.ArrayType.Index] = m.Indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    static Vector3[] Faces(MeshData m)
    {
        var f = new Vector3[m.Indices.Count];
        for (int i = 0; i < f.Length; i++)
        {
            int v = m.Indices[i];
            f[i] = new Vector3(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2]);
        }
        return f;
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
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -35, 0),
            LightEnergy = 1.3f,
            ShadowEnabled = medium,
            DirectionalShadowMaxDistance = 60,
        });
        GetViewport().ScreenSpaceAA = medium ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
    }
}
