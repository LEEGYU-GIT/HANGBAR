using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class UndergroundTests
{
    const int N = SceneContext.Size, Band = SceneContext.EdgeBand;

    static readonly Lazy<GeneratedWorld> World = new(() =>
        WorldGenerator.Generate(new TestScene(), WorldGeneratorTests.MeadowPalette, WorldGeneratorTests.Calm, WorldGeneratorTests.Photo, 7));

    /// <summary>The planned ground (after Shape and the edge band), before anything is dug.</summary>
    static readonly Lazy<SceneContext> Planned = new(() =>
    {
        var ctx = new SceneContext(Palette.EnsureContrast(WorldGeneratorTests.MeadowPalette), WorldGeneratorTests.Calm, WorldGeneratorTests.Photo, 7);
        new TestScene().Shape(ctx);
        EdgeBand.Apply(ctx);
        return ctx;
    });

    static int Surface(int x, int z) => Planned.Value.Surface[Planned.Value.Index(x, z)];
    static bool InBand(int x, int z) => Math.Min(Math.Min(x, z), Math.Min(N - 1 - x, N - 1 - z)) < Band;

    [Fact]
    public void Caves_are_carved()
    {
        var w = World.Value.World;
        int air = 0;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                for (int y = 2; y < Surface(x, z) - 3; y++)
                    if (w.Get(x, y, z) == Block.Air) air++;
        Assert.True(air >= 3000, $"{air} cave cells");
    }

    [Fact]
    public void Some_caves_open_to_the_sky()
    {
        var w = World.Value.World;
        var seen = new HashSet<(int, int, int)>();
        var queue = new Queue<(int X, int Y, int Z)>();
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                for (int y = 2; y < Surface(x, z) - 3; y++)
                    if (w.Get(x, y, z) == Block.Air && seen.Add((x, y, z))) queue.Enqueue((x, y, z));
        bool open = false;
        while (queue.Count > 0 && !open)
        {
            var (x, y, z) = queue.Dequeue();
            if (y >= Surface(x, z)) { open = true; break; }
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= N || nz >= N || ny < 0 || ny >= w.SizeY) continue;
                if (w.Get(nx, ny, nz) == Block.Air && seen.Add((nx, ny, nz))) queue.Enqueue((nx, ny, nz));
            }
        }
        Assert.True(open);
    }

    [Fact]
    public void No_caves_under_protected_columns()
    {
        var w = World.Value.World;
        var ctx = Planned.Value;
        int checkedColumns = 0;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                if (!ctx.NoCaves[ctx.Index(x, z)]) continue;
                checkedColumns++;
                for (int y = 2; y < Surface(x, z); y++) Assert.True(Block.IsSolid(w.Get(x, y, z)), $"hole at {x},{y},{z}");
            }
        Assert.True(checkedColumns > 0);
    }

    [Fact]
    public void No_caves_in_the_edge_band()
    {
        var w = World.Value.World;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                if (!InBand(x, z)) continue;
                for (int y = 2; y < Surface(x, z); y++) Assert.True(Block.IsSolid(w.Get(x, y, z)), $"hole at {x},{y},{z}");
            }
    }

    [Fact]
    public void Some_caves_hold_water()
    {
        var w = World.Value.World;
        int water = 0;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                for (int y = 2; y < Surface(x, z) - 3; y++)
                    if (Block.IsWater(w.Get(x, y, z))) water++;
        Assert.True(water >= 20, $"{water} groundwater cells");
    }

    [Fact]
    public void Twelve_burial_spots_inside_solid_ground()
    {
        var g = World.Value;
        var ctx = Planned.Value;
        Assert.Equal(12, g.BurialSpots.Count);
        foreach (var p in g.BurialSpots)
        {
            Assert.True(Block.IsSolid(g.World.Get(p.X, p.Y, p.Z)));
            Assert.InRange(Surface(p.X, p.Z) - p.Y, 3, 20);
            Assert.False(ctx.NoCaves[ctx.Index(p.X, p.Z)]);
            Assert.False(InBand(p.X, p.Z));
            foreach (var q in g.BurialSpots)
                if (q != p) Assert.True(Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Z - q.Z) * (p.Z - q.Z)) >= 8);
        }
    }

    [Fact]
    public void Bedrock_is_never_carved()
    {
        var w = World.Value.World;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                for (int y = 0; y < 2; y++) Assert.Equal(Material.Bedrock, Block.MaterialOf(w.Get(x, y, z)));
    }
}
