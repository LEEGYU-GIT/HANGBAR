using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class HiddenMuralTests
{
    const uint Red = 0xFF0000FF, Blue = 0x0000FFFF;

    /// <summary>Left half red, right half blue.</summary>
    static PixelImage Halves()
    {
        var px = new uint[64 * 48];
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 64; x++) px[y * 64 + x] = x < 32 ? Red : Blue;
        return new PixelImage(64, 48, px);
    }

    static bool Reddish(uint b) => (Block.ColorOf(b) >> 16 & 0xFF) > 200 && (Block.ColorOf(b) & 0xFF) < 60;
    static bool Bluish(uint b) => (Block.ColorOf(b) & 0xFF) > 200 && (Block.ColorOf(b) >> 16 & 0xFF) < 60;

    [Theory]
    [InlineData(Facing.North)] [InlineData(Facing.South)] [InlineData(Facing.East)] [InlineData(Facing.West)]
    public void Mural_reads_left_to_right_for_every_facing(Facing f)
    {
        var w = new VoxelWorld(64, 32, 64);
        var spot = new MuralSpot(new BlockPos(32, 2, 32), f, 16, 12, false);
        Mural.Stamp(w, Halves(), spot);
        var right = HiddenMural.RightOf(f);
        uint left = w.Get(32, 2, 32), farRight = w.Get(32 + right.X * 15, 2, 32 + right.Z * 15);
        Assert.True(Reddish(left), $"{f} left");
        Assert.True(Bluish(farRight), $"{f} right");
        Assert.Equal(Material.Pixel, Block.MaterialOf(left));
    }

    [Fact]
    public void Framed_mural_has_a_wooden_border()
    {
        var w = new VoxelWorld(64, 32, 64);
        Mural.Stamp(w, Halves(), new MuralSpot(new BlockPos(32, 4, 32), Facing.South, 16, 12, true), 0x3A2A1A);
        Assert.Equal(Material.Wood, Block.MaterialOf(w.Get(31, 3, 32)));   // bottom-left corner of the frame
        Assert.Equal(Material.Wood, Block.MaterialOf(w.Get(48, 16, 32)));  // top-right corner
        Assert.Equal(Material.Pixel, Block.MaterialOf(w.Get(32, 4, 32)));
    }

    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 20).Select(s => new object[] { (ulong)s });

    [Theory, MemberData(nameof(Seeds))]
    public void Mural_is_hidden_from_the_spawn(ulong seed)
    {
        var g = WeatheringTests.Make(seed);
        Assert.True(HiddenMural.IsHidden(g.World, g.Spawn, g.Mural));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Mural_is_far_from_the_spawn(ulong seed)
    {
        var g = WeatheringTests.Make(seed);
        Assert.True(HiddenMural.Distance(g.Spawn, g.Mural) >= 40);
    }

    [Theory]
    [InlineData(1UL)] [InlineData(2UL)] [InlineData(3UL)]
    public void Mural_can_be_reached_on_foot(ulong seed)
    {
        var g = WeatheringTests.Make(seed);
        Assert.True(HiddenMural.IsReachable(Walkability.ReachCells(g.World, g.Spawn), g.Mural));
        Assert.Equal(Material.Pixel, Block.MaterialOf(g.World.Get(g.Mural.Origin.X, g.Mural.Origin.Y, g.Mural.Origin.Z)));
    }

    [Theory]
    [InlineData(1UL)] [InlineData(4UL)] [InlineData(9UL)]
    public void Fallback_wall_when_no_candidate_works(ulong seed)
    {
        var g = WeatheringTests.Make(seed, withCandidate: false);
        Assert.Equal((24, 18), (g.Mural.Width, g.Mural.Height));
        Assert.True(HiddenMural.IsHidden(g.World, g.Spawn, g.Mural));
        Assert.True(HiddenMural.Distance(g.Spawn, g.Mural) >= 40);
        Assert.True(HiddenMural.IsReachable(Walkability.ReachCells(g.World, g.Spawn), g.Mural));
    }
}
