using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class PhotoTraitsTests
{
    static readonly PixelImage Meadow = ImageFit.Prepare(SamplePhotos.Meadow(), 192);

    static double Luma(uint rgb) =>
        (0.299 * ((rgb >> 16) & 0xFF) + 0.587 * ((rgb >> 8) & 0xFF) + 0.114 * (rgb & 0xFF)) / 255.0;

    static bool Near(uint rgb, int r, int g, int b) =>
        Math.Abs((int)((rgb >> 16) & 0xFF) - r) <= 8 && Math.Abs((int)((rgb >> 8) & 0xFF) - g) <= 8 && Math.Abs((int)(rgb & 0xFF) - b) <= 8;

    [Fact]
    public void Two_color_photo_gives_both_colors()
    {
        var px = new uint[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = i % 64 < 32 ? PixelImage.Pack(220, 40, 40) : PixelImage.Pack(40, 60, 200);
        var p = Palette.Extract(new PixelImage(64, 64, px), 2);
        Assert.Equal(2, p.Length);
        Assert.Contains(p, c => Near(c, 220, 40, 40));
        Assert.Contains(p, c => Near(c, 40, 60, 200));
    }

    [Fact]
    public void Palette_has_the_requested_count_and_no_alpha()
    {
        var p = Palette.Extract(Meadow);
        Assert.Equal(6, p.Length);
        Assert.All(p, c => Assert.Equal(0u, c >> 24));
    }

    [Fact]
    public void Palette_is_sorted_dark_to_light()
    {
        var lumas = Palette.Extract(Meadow).Select(Luma).ToArray();
        Assert.Equal(lumas.OrderBy(l => l), lumas);
        Assert.True(lumas[^1] - lumas[0] > 0.1, "a meadow with a pond and rocks has more than one shade");
    }

    [Fact]
    public void Palette_is_deterministic() => Assert.Equal(Palette.Extract(Meadow), Palette.Extract(ImageFit.Prepare(SamplePhotos.Meadow(), 192)));

    [Fact]
    public void Gray_photo_palette_keeps_contrast()
    {
        var p = Palette.EnsureContrast(Palette.Extract(BiomeTests.Solid(64, 128, 128, 128)));
        Assert.Equal(6, p.Length);
        Assert.True(p.Max(Luma) - p.Min(Luma) >= 0.35, $"contrast {p.Max(Luma) - p.Min(Luma):0.000}");
    }

    [Fact]
    public void Black_photo_palette_gets_a_light_end()
    {
        var p = Palette.EnsureContrast(Palette.Extract(BiomeTests.Solid(64, 0, 0, 0)));
        Assert.True(p.Max(Luma) - p.Min(Luma) >= 0.35);
    }

    [Fact]
    public void Palette_with_contrast_is_left_alone()
    {
        uint[] p = { 0x101010, 0x406020, 0xE0E0D0 };
        Assert.Equal(p, Palette.EnsureContrast(p));
    }

    [Fact]
    public void Night_photo_is_dark_and_warm_photo_is_warm()
    {
        Assert.True(MoodReader.Read(BiomeTests.Solid(32, 20, 20, 30)).Brightness < 0.15);
        Assert.True(MoodReader.Read(BiomeTests.Solid(32, 230, 150, 60)).Warmth > 0.6);
    }

    [Fact]
    public void Gray_photo_is_unsaturated_and_neutral()
    {
        var m = MoodReader.Read(BiomeTests.Solid(32, 128, 128, 128));
        Assert.True(m.Saturation < 0.05);
        Assert.InRange(m.Warmth, 0.49f, 0.51f);
        Assert.InRange(m.Brightness, 0.45f, 0.55f);
    }

    [Fact]
    public void Blue_photo_is_cool_and_saturated()
    {
        var m = MoodReader.Read(BiomeTests.Solid(32, 40, 90, 200));
        Assert.True(m.Warmth < 0.3);
        Assert.True(m.Saturation > 0.7);
    }
}
