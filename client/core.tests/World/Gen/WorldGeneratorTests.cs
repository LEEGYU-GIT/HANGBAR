using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class WorldGeneratorTests
{
    internal static readonly PixelImage Photo = ImageFit.Prepare(SamplePhotos.Meadow(), 192);
    internal static readonly uint[] MeadowPalette = Palette.Extract(Photo);
    internal static readonly Mood Calm = new(0.6f, 0.4f, 0.5f);

    static GeneratedWorld Make(ulong seed = 7, float horizon = 0.5f, uint[]? palette = null, PixelImage? photo = null) =>
        WorldGenerator.Generate(new TestScene(), palette ?? MeadowPalette, Calm, photo ?? Photo, seed, horizon);

    static readonly Lazy<GeneratedWorld> Default = new(() => Make());

    static int SurfaceOf(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }

    [Fact]
    public void World_is_192_by_160_by_192()
    {
        var w = Default.Value.World;
        Assert.Equal((192, 160, 192), (w.SizeX, w.SizeY, w.SizeZ));
    }

    [Fact]
    public void Bottom_two_layers_are_bedrock()
    {
        var w = Default.Value.World;
        for (int z = 0; z < 192; z += 7)
            for (int x = 0; x < 192; x += 7)
            {
                Assert.Equal(Material.Bedrock, Block.MaterialOf(w.Get(x, 0, z)));
                Assert.Equal(Material.Bedrock, Block.MaterialOf(w.Get(x, 1, z)));
            }
    }

    [Fact]
    public void Layers_go_top_then_soil_then_stone_then_deep_stone()
    {
        var w = Default.Value.World;
        int checkedColumns = 0;
        for (int z = 20; z < 172; z += 5)
            for (int x = 20; x < 172; x += 5)
            {
                int s = SurfaceOf(w, x, z);
                if (s < 60 || Default.Value.Structures.Any(p => x >= p.Min.X && x <= p.Max.X && z >= p.Min.Z && z <= p.Max.Z)) continue;
                bool dug = false; // caves (Task 7) cut through some columns: check only untouched ones
                for (int y = 2; y < s && !dug; y++) dug = !Block.IsSolid(w.Get(x, y, z));
                if (dug) continue;
                checkedColumns++;
                Assert.Equal(Material.Grass, Block.MaterialOf(w.Get(x, s - 1, z)));
                for (int k = 2; k <= 4; k++) Assert.Equal(Material.Soil, Block.MaterialOf(w.Get(x, s - k, z)));
                Assert.Equal(Material.Stone, Block.MaterialOf(w.Get(x, 30, z)));
                Assert.Equal(Material.DeepStone, Block.MaterialOf(w.Get(x, 10, z)));
            }
        Assert.True(checkedColumns > 50);
    }

    [Fact]
    public void Edges_settle_to_base_ground()
    {
        var g = Default.Value;
        for (int i = 0; i < 192; i++)
            foreach (var (x, z) in new[] { (i, 0), (0, i), (i, 191), (191, i) })
            {
                Assert.Equal(56, SurfaceOf(g.World, x, z));
                Assert.NotEqual(Block.Water, g.World.Get(x, 56, z));
            }
        // just inside the band the scene's own height stands: compare with a world built without the edge band
        var raw = new SceneContext(Palette.EnsureContrast(MeadowPalette), Calm, Photo, 7);
        new TestScene().Shape(raw);
        Assert.Equal(raw.Surface[raw.Index(100, 16)], SurfaceOf(g.World, 100, 16));
    }

    [Fact]
    public void Same_inputs_give_the_same_world()
    {
        var a = Make(11);
        var b = Make(11);
        Assert.Equal(Hash(a.World), Hash(b.World));
        Assert.Equal(a.Spawn, b.Spawn);
        Assert.Equal(a.Props, b.Props);
        Assert.Equal(a.Structures.Select(s => (s.Name, s.Min, s.Max)), b.Structures.Select(s => (s.Name, s.Min, s.Max)));
        Assert.NotEqual(Hash(a.World), Hash(Make(12).World));
    }

    internal static ulong Hash(VoxelWorld w)
    {
        ulong h = 14695981039346656037UL;
        for (int y = 0; y < w.SizeY; y++)
            for (int z = 0; z < w.SizeZ; z++)
                for (int x = 0; x < w.SizeX; x++) { h ^= w.Get(x, y, z); h *= 1099511628211UL; }
        return h;
    }

    [Fact]
    public void Spawn_is_open_ground()
    {
        var g = Default.Value;
        Assert.True(Walkability.IsStandable(g.World, g.Spawn.X, g.Spawn.Y, g.Spawn.Z));
    }

    [Fact]
    public void Facing_points_at_the_view_target()
    {
        var g = Default.Value; // target is 30 cells north (−z): Godot yaw 0
        Assert.InRange(g.FacingDegrees, -1f, 1f);
    }

    [Fact]
    public void Pitch_follows_the_photo_horizon() => Assert.Equal(-10f, Make(horizon: 0.3f).PitchDegrees, 3);

    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(0, 0, 0)]
    public void Extreme_palettes_keep_materials_apart(int r, int g, int b)
    {
        var photo = new PixelImage(64, 64, Enumerable.Repeat(PixelImage.Pack(r, g, b), 64 * 64).ToArray());
        var ctx = new SceneContext(Palette.EnsureContrast(Palette.Extract(ImageFit.Prepare(photo, 192))), Calm, photo, 1);
        double grass = Palette.Luma(ctx.ColorOf(Material.Grass)), soil = Palette.Luma(ctx.ColorOf(Material.Soil)), stone = Palette.Luma(ctx.ColorOf(Material.Stone));
        Assert.True(Math.Abs(grass - soil) >= 0.06, $"grass {grass} soil {soil}");
        Assert.True(Math.Abs(grass - stone) >= 0.06, $"grass {grass} stone {stone}");
        Assert.True(Math.Abs(soil - stone) >= 0.06, $"soil {soil} stone {stone}");
    }

    [Fact]
    public void Tiny_photo_generates()
    {
        var tiny = ImageFit.Prepare(new PixelImage(1, 1, new[] { 0x336699FFu }), 192);
        var clear = ImageFit.Prepare(new PixelImage(4, 4, new uint[16]), 192);
        foreach (var p in new[] { tiny, clear })
        {
            var g = WorldGenerator.Generate(new TestScene(), Palette.Extract(p), Calm, p, 3, Horizon.Estimate(p));
            Assert.NotNull(g.World);
        }
    }

    [Fact]
    public void Unknown_scenes_are_not_built_yet_and_names_parse()
    {
        Assert.Equal(Scene.Beach, Scenes.FromName("BEACH"));
        Assert.Null(Scenes.FromName("castle"));
        Assert.Equal(Biome.Indoor, Scenes.BiomeOf(Scene.House));
    }
}

public class HorizonTests
{
    static PixelImage SkyOverGrass(int skyRows, int height = 96)
    {
        var px = new uint[96 * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < 96; x++)
                px[y * 96 + x] = y < skyRows ? PixelImage.Pack(150, 190, 240) : PixelImage.Pack(80, 150, 60);
        return new PixelImage(96, height, px);
    }

    [Fact]
    public void Sky_over_grass_horizon_is_a_third() => Assert.InRange(Horizon.Estimate(SkyOverGrass(32)), 0.28f, 0.38f);

    [Fact]
    public void Photo_without_sky_is_the_middle()
    {
        Assert.Equal(0.5f, Horizon.Estimate(SkyOverGrass(0)));
        Assert.Equal(0.5f, Horizon.Estimate(SkyOverGrass(96))); // all sky
    }

    [Fact]
    public void Pitch_is_negative_when_the_horizon_is_high() => Assert.True(Horizon.PitchDegrees(0.2f) < 0);

    [Fact]
    public void Pitch_is_clamped()
    {
        Assert.Equal(-25f, Horizon.PitchDegrees(0f));
        Assert.Equal(15f, Horizon.PitchDegrees(1f));
    }
}
