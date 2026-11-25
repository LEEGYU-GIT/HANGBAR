using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class WeatheringTests
{
    const int N = SceneContext.Size, C = TestScene.C;

    internal static GeneratedWorld Make(ulong seed = 7, bool withCandidate = true) =>
        WorldGenerator.Generate(new TestScene(withCandidate), WorldGeneratorTests.MeadowPalette, WorldGeneratorTests.Calm, WorldGeneratorTests.Photo, seed);

    static readonly Lazy<GeneratedWorld> World = new(() => Make());

    static int Top(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y;
        return -1;
    }

    [Fact]
    public void Old_structures_crack_about_a_third()
    {
        var g = World.Value;
        var steps = g.Structures.Single(s => s.Name == "stone_steps");
        int solid = 0, cracked = 0;
        for (int y = steps.Min.Y; y <= steps.Max.Y; y++)
            for (int z = steps.Min.Z; z <= steps.Max.Z; z++)
                for (int x = steps.Min.X; x <= steps.Max.X; x++)
                {
                    uint b = g.World.Get(x, y, z);
                    if (Block.MaterialOf(b) != Material.Stone) continue;
                    solid++;
                    if (Block.StateOf(b) == BlockState.Cracked) cracked++;
                }
        Assert.InRange(cracked / (double)solid, 0.25, 0.35);
    }

    [Fact]
    public void Paths_wear()
    {
        var w = World.Value.World;
        int worn = 0, total = 0;
        for (int x = 40; x < 150; x++)
        {
            uint b = w.Get(x, Top(w, x, C + 10), C + 10);
            total++;
            if (Block.StateOf(b) == BlockState.Worn) worn++;
        }
        Assert.True(worn / (double)total >= 0.6, $"{worn}/{total}");
    }

    [Fact]
    public void Paths_count_as_stairs()
    {
        // a trail is walked without jumping: its steps lift you like stairs (user decision 2026-11-25)
        var g = World.Value;
        for (int x = 40; x < 150; x++) Assert.True(g.Stairs[(C + 10) * N + x], $"trail column {x} is not a stair");
        Assert.False(g.Stairs[(C - 40) * N + C], "open ground is not a stair");
    }

    [Fact]
    public void Ground_by_water_gets_moss()
    {
        var g = World.Value;
        var map = g.Surface;
        int mossy = 0, total = 0;
        for (int z = 1; z < N - 1; z++)
            for (int x = 1; x < N - 1; x++)
            {
                if (map.Water[map.Index(x, z)]) continue;
                bool nearWater = false;
                for (int dz = -2; dz <= 2 && !nearWater; dz++)
                    for (int dx = -2; dx <= 2 && !nearWater; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        nearWater = nx >= 0 && nz >= 0 && nx < N && nz < N && map.Water[map.Index(nx, nz)];
                    }
                if (!nearWater) continue;
                total++;
                if (Block.StateOf(g.World.Get(x, Top(g.World, x, z), z)) == BlockState.Mossy) mossy++;
            }
        Assert.True(total > 50);
        Assert.True(mossy / (double)total >= 0.3, $"{mossy}/{total}");
    }

    [Fact]
    public void Photo_and_glass_blocks_never_weather()
    {
        var g = World.Value;
        int pixels = 0, glass = 0;
        for (int y = 0; y < g.World.SizeY; y++)
            for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                {
                    uint b = g.World.Get(x, y, z);
                    var m = Block.MaterialOf(b);
                    if (m is not (Material.Pixel or Material.Glass or Material.Bedrock)) continue;
                    Assert.Equal(BlockState.Intact, Block.StateOf(b));
                    if (m == Material.Pixel) pixels++;
                    if (m == Material.Glass) glass++;
                }
        Assert.True(pixels > 100 && glass >= 1);
    }

    [Fact]
    public void Same_seed_weathers_the_same() =>
        Assert.Equal(WorldGeneratorTests.Hash(Make(5).World), WorldGeneratorTests.Hash(Make(5).World));
}
