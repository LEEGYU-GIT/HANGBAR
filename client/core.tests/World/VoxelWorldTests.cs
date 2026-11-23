using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class VoxelWorldTests
{
    static FileMap OneColumn(int height, uint color, bool water = false) =>
        new(1, 8, 0, new[] { height }, new[] { color }, new[] { water });

    static void AssertColorNear(uint expected, uint actual)
    {
        for (int s = 0; s <= 16; s += 8)
            Assert.InRange((int)((actual >> s) & 0xFF) - (int)((expected >> s) & 0xFF), -7, 7);
    }

    [Fact]
    public void Solid_roundtrips_color_within_5bit_precision()
    {
        var b = Block.Solid(0xC86432);
        Assert.True(Block.IsSolid(b));
        AssertColorNear(0xC86432, Block.ColorOf(b));
        Assert.False(Block.IsSolid(Block.Air));
        Assert.False(Block.IsSolid(Block.Water));
    }

    [Fact]
    public void Out_of_bounds_reads_air()
    {
        var w = new VoxelWorld(16, 16, 16);
        w.Set(0, 0, 0, Block.Solid(0xFFFFFF));
        Assert.Equal(Block.Air, w.Get(-1, 0, 0));
        Assert.Equal(Block.Air, w.Get(0, 16, 0));
        w.Set(99, 0, 0, Block.Water); // ignored, no throw
    }

    [Fact]
    public void Set_then_get_across_chunk_boundary()
    {
        var w = new VoxelWorld(40, 20, 40);
        w.Set(15, 0, 0, 2);
        w.Set(16, 0, 0, 3);
        w.Set(39, 19, 39, 4);
        Assert.Equal((ushort)2, w.Get(15, 0, 0));
        Assert.Equal((ushort)3, w.Get(16, 0, 0));
        Assert.Equal((ushort)4, w.Get(39, 19, 39));
        Assert.Equal((3, 2, 3), (w.ChunksX, w.ChunksY, w.ChunksZ));
    }

    [Fact]
    public void FromFileMap_layers_surface_dirt_rock()
    {
        var w = VoxelWorld.FromFileMap(OneColumn(10, 0x80C040));
        Assert.Equal(Block.Solid(0x80C040), w.Get(0, 9, 0));
        for (int y = 6; y <= 8; y++) AssertColorNear(0x4C7326, Block.ColorOf(w.Get(0, y, 0))); // ×0.6
        for (int y = 0; y <= 5; y++) AssertColorNear(0x61774A,Block.ColorOf(w.Get(0, y, 0))); // ×0.35 + 0x50×0.65
        Assert.Equal(Block.Air, w.Get(0, 10, 0));
    }

    [Fact]
    public void FromFileMap_fills_water_up_to_level()
    {
        var w = VoxelWorld.FromFileMap(OneColumn(6, 0x3060C0, water: true));
        Assert.True(Block.IsSolid(w.Get(0, 5, 0)));
        Assert.Equal(Block.Water, w.Get(0, 6, 0));
        Assert.Equal(Block.Water, w.Get(0, 7, 0));
        Assert.Equal(Block.Air, w.Get(0, 8, 0));
    }

    [Fact]
    public void Empty_chunks_are_not_allocated()
    {
        var map = new FileMap(32, 8, 0, Enumerable.Repeat(8, 32 * 32).ToArray(), new uint[32 * 32], new bool[32 * 32]);
        var w = VoxelWorld.FromFileMap(map); // 32×48×32 → 2×3×2 chunks, only the bottom layer has blocks
        Assert.Equal(48, w.SizeY);
        Assert.Equal(4, w.AllocatedChunks);
    }
}
