using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class BlockTests
{
    [Fact]
    public void Color_is_kept_exactly() => Assert.Equal(0xC86432u, Block.ColorOf(Block.Solid(0xC86432)));

    [Fact]
    public void Material_and_state_roundtrip()
    {
        for (var m = Material.Pixel; m <= Material.Cloth; m++)
            for (var s = BlockState.Intact; s <= BlockState.Worn; s++)
            {
                uint b = Block.Make(m, 0x123456, s);
                Assert.Equal(m, Block.MaterialOf(b));
                Assert.Equal(s, Block.StateOf(b));
                Assert.Equal(0x123456u, Block.ColorOf(b));
            }
    }

    [Fact]
    public void Air_and_water_are_not_solid()
    {
        Assert.False(Block.IsSolid(Block.Air));
        Assert.False(Block.IsSolid(Block.Water));
        Assert.True(Block.IsWater(Block.Make(Material.Water, 0x3366AA)));
        Assert.True(Block.IsWater(Block.Water));
        Assert.False(Block.IsWater(Block.Air));
    }

    [Fact]
    public void Glass_is_solid_but_see_through()
    {
        uint glass = Block.Solid(0xC8E6F0, Material.Glass);
        Assert.True(Block.IsSolid(glass));
        Assert.False(Block.IsOpaque(glass));
        Assert.True(Block.IsOpaque(Block.Solid(0x202830, Material.DarkGlass)));
        Assert.True(Block.IsOpaque(Block.Solid(0x808080, Material.Stone)));
    }

    [Fact]
    public void Bedrock_cannot_be_broken()
    {
        Assert.False(Block.IsBreakable(Block.Solid(0x2A2A2E, Material.Bedrock)));
        Assert.True(Block.IsBreakable(Block.Solid(0x808080, Material.Stone)));
        Assert.False(Block.IsBreakable(Block.Air));
    }

    [Fact]
    public void WithState_changes_only_the_state()
    {
        uint b = Block.WithState(Block.Solid(0xABCDEF, Material.Stone), BlockState.Cracked);
        Assert.Equal(Material.Stone, Block.MaterialOf(b));
        Assert.Equal(BlockState.Cracked, Block.StateOf(b));
        Assert.Equal(0xABCDEFu, Block.ColorOf(b));
    }

    [Fact]
    public void Solid_refuses_air_and_water()
    {
        Assert.Throws<ArgumentException>(() => Block.Solid(0, Material.Water));
        Assert.Throws<ArgumentException>(() => Block.Solid(0, Material.Air));
    }
}
