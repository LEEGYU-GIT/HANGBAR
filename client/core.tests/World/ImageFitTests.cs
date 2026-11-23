using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class ImageFitTests
{
    const uint Red = 0xFF0000FF, Green = 0x00FF00FF, Blue = 0x0000FFFF, Black = 0x000000FF, White = 0xFFFFFFFF;

    static PixelImage Solid(int w, int h, uint c) => new(w, h, Enumerable.Repeat(c, w * h).ToArray());

    static (int R, int G, int B) Rgb(uint p) => ((int)(p >> 24) & 0xFF, (int)(p >> 16) & 0xFF, (int)(p >> 8) & 0xFF);

    [Fact]
    public void Prepare_crops_landscape_to_center_square()
    {
        var src = new PixelImage(4, 2, new[] { Red, Green, Green, Red, Red, Green, Green, Red });
        var p = ImageFit.Prepare(src, 2);
        Assert.Equal(2, p.Width);
        Assert.All(p.Rgba, px => Assert.Equal(Green, px));
    }

    [Fact]
    public void Prepare_downscales_by_averaging()
    {
        var p = ImageFit.Prepare(new PixelImage(2, 2, new[] { Black, White, Black, White }), 1);
        var (r, g, b) = Rgb(p[0, 0]);
        Assert.InRange(r, 127, 128);
        Assert.InRange(g, 127, 128);
        Assert.InRange(b, 127, 128);
    }

    [Fact]
    public void Prepare_upscales_tiny_image()
    {
        var p = ImageFit.Prepare(Solid(1, 1, Blue), 192);
        Assert.Equal((192, 192), (p.Width, p.Height));
        Assert.All(p.Rgba, px => Assert.Equal(Blue, px));
    }

    [Theory]
    [InlineData(8000, 1000)]
    [InlineData(4000, 3000)]
    [InlineData(1000, 8000)]
    public void Prepare_handles_panorama_and_phone_sizes(int w, int h)
    {
        var p = ImageFit.Prepare(Solid(w, h, Red), 192);
        Assert.Equal((192, 192), (p.Width, p.Height));
        Assert.Equal(Red, p[100, 100]);
    }

    [Fact]
    public void Transparent_pixels_become_light_gray()
    {
        var p = ImageFit.Prepare(new PixelImage(2, 2, new[] { 0x12345600u, 0xFF0000C8u, 0x12345600u, 0xFF0000C8u }), 2);
        Assert.Equal(ImageFit.TransparentFill, p[0, 0]);
        Assert.Equal(0xD8D8D8FFu, ImageFit.TransparentFill);
        Assert.Equal(Red, p[1, 0]);
    }

    [Fact]
    public void Pixel_array_must_match_size() =>
        Assert.Throws<ArgumentException>(() => new PixelImage(2, 2, new uint[3]));

    [Fact]
    public void Meadow_is_deterministic() => Assert.Equal(SamplePhotos.Meadow(64).Rgba, SamplePhotos.Meadow(64).Rgba);
}
