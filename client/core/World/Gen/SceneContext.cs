namespace Hangbar.Core.World.Gen;

/// <summary>Which way something faces. North looks toward −z.</summary>
public enum Facing { North, South, East, West }

/// <summary>A place for the photo mural. Origin is the bottom-left block as seen by someone standing in front of it.</summary>
public sealed record MuralSpot(BlockPos Origin, Facing Facing, int Width, int Height, bool Framed);

/// <summary>The scene's big sight (v2 design: one landmark per scene variant).</summary>
public sealed record Landmark(string Name, BlockPos Center);

/// <summary>Where the player starts, what they look at first, and where the mural may go.</summary>
public sealed record SceneView(BlockPos Spawn, BlockPos Target, IReadOnlyList<MuralSpot> MuralCandidates);

/// <summary>
/// One scene variant. Shape fills the column arrays (surface, water, top material, protected columns, paths);
/// Build places structures, props and the landmark into the filled world; View says where the player starts.
/// </summary>
public interface IScene
{
    Scene Kind { get; }
    void Shape(SceneContext ctx);
    void Build(SceneContext ctx);
    SceneView View(SceneContext ctx);
}

/// <summary>Everything a scene works with while one world is generated.</summary>
public sealed class SceneContext
{
    public const int Size = 192, Height = 160, BaseGround = 56, EdgeBand = 16, WaterLevel = 56;

    public uint[] Palette { get; }
    public PaletteRoles Colors { get; }
    public Mood Mood { get; }
    public PixelImage Photo { get; }
    public ulong Seed { get; }
    public Rng Rng { get; }
    public ValueNoise Noise { get; }

    /// <summary>Per column: the first air block above the ground (top solid block at Surface − 1).</summary>
    public int[] Surface { get; } = Enumerable.Repeat(BaseGround, Size * Size).ToArray();
    /// <summary>Per column: water fills Surface..WaterTop−1; 0 = no water.</summary>
    public int[] WaterTop { get; } = new int[Size * Size];
    public Material[] TopMaterial { get; } = Enumerable.Repeat(Material.Grass, Size * Size).ToArray();
    /// <summary>Columns that caves must not touch (spawn yard, building plots).</summary>
    public bool[] NoCaves { get; } = new bool[Size * Size];
    public bool[] Path { get; } = new bool[Size * Size];

    public VoxelWorld World { get; internal set; } = null!;
    public List<Prop> Props { get; } = new();
    public List<PlacedPart> Structures { get; } = new();
    public List<BlockPos> BurialSpots { get; } = new();
    public Landmark? Landmark { get; set; }

    readonly List<PlacedPart> _old = new();
    readonly Dictionary<Material, uint> _materialColors;

    public SceneContext(uint[] palette, Mood mood, PixelImage photo, ulong seed)
    {
        Palette = palette;
        Colors = PaletteRoles.From(palette);
        Mood = mood;
        Photo = photo;
        Seed = seed;
        Rng = new Rng(seed ^ 0x5CE7E5EEDUL);
        Noise = new ValueNoise(Rng.Mix(seed));
        _materialColors = Strata.MaterialColors(Colors);
    }

    public int Index(int x, int z) => z * Size + x;

    /// <summary>The base colour the ground layers use for a material (kept apart in brightness, v2 Review Focus 1).</summary>
    public uint ColorOf(Material m) => _materialColors.TryGetValue(m, out var c) ? c : Colors.Primary;

    /// <summary>Marks a structure as old: weathering cracks a share of its blocks.</summary>
    public void MarkOld(PlacedPart p) => _old.Add(p);

    public IReadOnlyList<PlacedPart> OldParts => _old;

    public bool IsOld(int x, int y, int z) =>
        _old.Any(p => x >= p.Min.X && x <= p.Max.X && y >= p.Min.Y && y <= p.Max.Y && z >= p.Min.Z && z <= p.Max.Z);
}

/// <summary>The result of the v2 generator.</summary>
/// <param name="FacingDegrees">Godot yaw: forward is (−sin, 0, −cos).</param>
/// <param name="PitchDegrees">Negative looks down.</param>
public sealed record GeneratedWorld(
    VoxelWorld World, FileMap Surface, Scene Scene, Mood Mood, ulong Seed, BlockPos Spawn, float FacingDegrees, float PitchDegrees,
    IReadOnlyList<Prop> Props, IReadOnlyList<PlacedPart> Structures, IReadOnlyList<BlockPos> BurialSpots, Landmark Landmark, MuralSpot Mural);
