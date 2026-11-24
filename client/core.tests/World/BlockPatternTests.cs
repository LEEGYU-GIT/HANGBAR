using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class BlockPatternTests
{
    const int N = BlockPattern.Pixels;

    static Texel[] All(Material m, BlockState s, uint hash)
    {
        var t = new Texel[N * N];
        for (int py = 0; py < N; py++)
            for (int px = 0; px < N; px++)
                t[py * N + px] = BlockPattern.Sample(m, s, px, py, hash);
        return t;
    }

    static int Differ(Texel[] a, Texel[] b, float by) => a.Zip(b).Count(p => Math.Abs(p.First.Shade - p.Second.Shade) > by);

    static readonly Material[] Patterned = Enum.GetValues<Material>().Where(m => m >= Material.Soil).ToArray();

    [Fact]
    public void Pixel_material_is_flat() =>
        Assert.All(All(Material.Pixel, BlockState.Intact, 12345), t => Assert.Equal(new Texel(1f, 0f), t));

    [Fact]
    public void Patterns_keep_the_block_colour()
    {
        foreach (var m in Patterned)
            foreach (uint h in new uint[] { 1, 77, 0xDEADBEEF })
            {
                var t = All(m, BlockState.Intact, h);
                Assert.InRange(t.Average(x => x.Shade), 0.9, 1.1);
                Assert.All(t, x => Assert.InRange(x.Shade, 0.6f, 1.3f));
                Assert.All(t, x => Assert.Equal(0f, x.Moss));
            }
    }

    [Theory]
    [InlineData(Material.Stone, Material.Wood)]
    [InlineData(Material.Brick, Material.Tile)]
    [InlineData(Material.Grass, Material.Sand)]
    [InlineData(Material.Soil, Material.Concrete)]
    public void Materials_look_different(Material a, Material b) =>
        Assert.True(Differ(All(a, BlockState.Intact, 99), All(b, BlockState.Intact, 99), 0.03f) >= 16);

    [Theory]
    [InlineData(Material.Stone)] [InlineData(Material.DeepStone)] [InlineData(Material.Bedrock)] [InlineData(Material.Concrete)]
    public void Stone_like_patterns_are_blocky(Material m)
    {
        var t = All(m, BlockState.Intact, 4242);
        for (int py = 0; py < N; py += 2)
            for (int px = 0; px < N; px += 2)
            {
                float s = t[py * N + px].Shade;
                Assert.Equal(s, t[py * N + px + 1].Shade);
                Assert.Equal(s, t[(py + 1) * N + px].Shade);
                Assert.Equal(s, t[(py + 1) * N + px + 1].Shade);
            }
    }

    [Fact]
    public void Same_material_varies_by_block() =>
        Assert.True(Differ(All(Material.Stone, BlockState.Intact, 1), All(Material.Stone, BlockState.Intact, 2), 0.001f) >= 8);

    [Fact]
    public void Cracked_adds_dark_pixels()
    {
        var intact = All(Material.Stone, BlockState.Intact, 555);
        var cracked = All(Material.Stone, BlockState.Cracked, 555);
        Assert.True(intact.Zip(cracked).Count(p => p.First.Shade - p.Second.Shade >= 0.15f) >= 6);
    }

    [Fact]
    public void Mossy_tints_from_the_top()
    {
        var t = All(Material.Stone, BlockState.Mossy, 31);
        Assert.True(t.Count(x => x.Moss > 0) >= 12);
        Assert.True(t.Take(2 * N).Count(x => x.Moss > 0) >= 8);
    }

    [Theory]
    [InlineData(Material.Stone)] [InlineData(Material.Brick)] [InlineData(Material.Wood)]
    public void Worn_lowers_contrast(Material m)
    {
        static float Contrast(Texel[] t) => t.Max(x => x.Shade) - t.Min(x => x.Shade);
        Assert.True(Contrast(All(m, BlockState.Worn, 8)) < Contrast(All(m, BlockState.Intact, 8)));
    }

    [Fact]
    public void Hash_matches_the_fixed_formula()
    {
        uint h = unchecked(3u * 73856093u ^ 4u * 19349663u ^ 5u * 83492791u ^ 2u * 2654435761u);
        h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13;
        Assert.Equal(h, BlockPattern.Hash(3, 4, 5, 2));
    }
}
