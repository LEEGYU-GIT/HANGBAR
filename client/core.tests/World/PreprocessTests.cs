using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class PreprocessTests
{
    static readonly float[] Half = { 0.5f, 0.5f, 0.5f };

    [Fact]
    public void ToChw_has_3_planes_of_size_squared() =>
        Assert.Equal(3 * 224 * 224, Preprocess.ToChw(BiomeTests.Solid(10, 30, 60, 90), 224, Half, Half).Length);

    [Fact]
    public void ToChw_normalizes_white() =>
        Assert.All(Preprocess.ToChw(BiomeTests.Solid(32, 255, 255, 255), 16, Half, Half), v => Assert.Equal(1f, v, 4));

    [Fact]
    public void ToChw_is_channel_first()
    {
        var t = Preprocess.ToChw(BiomeTests.Solid(32, 255, 0, 0), 8, Half, Half);
        int plane = 8 * 8;
        Assert.Equal(1f, t[0], 4);          // red plane
        Assert.Equal(-1f, t[plane], 4);     // green plane
        Assert.Equal(-1f, t[2 * plane], 4); // blue plane
    }

    [Fact]
    public void ToChw_crops_the_middle_square()
    {
        // a wide photo: left and right thirds black, middle white → the square crop is all white
        var px = new uint[30 * 10];
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 30; x++) px[y * 30 + x] = x is >= 10 and < 20 ? PixelImage.Pack(255, 255, 255) : PixelImage.Pack(0, 0, 0);
        Assert.All(Preprocess.ToChw(new PixelImage(30, 10, px), 8, Half, Half), v => Assert.Equal(1f, v, 2));
    }
}
