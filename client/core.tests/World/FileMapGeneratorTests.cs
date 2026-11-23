using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class FileMapGeneratorTests
{
    static readonly MapGenSettings S = new();

    static PixelImage Solid(int size, uint c) => new(size, size, Enumerable.Repeat(c, size * size).ToArray());

    [Fact]
    public void Same_photo_gives_same_map()
    {
        var a = FileMapGenerator.Generate(SamplePhotos.Meadow(256), S);
        var b = FileMapGenerator.Generate(SamplePhotos.Meadow(256), S);
        Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Color, b.Color);
        Assert.Equal(a.Water, b.Water);
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(192, a.Size);
    }

    [Fact]
    public void Different_photos_give_different_seeds()
    {
        var photo = SamplePhotos.Meadow(256);
        var copy = (uint[])photo.Rgba.Clone();
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                copy[y * 256 + x] = 0x000000FF;
        Assert.NotEqual(FileMapGenerator.Generate(photo, S).Seed, FileMapGenerator.Generate(new PixelImage(256, 256, copy), S).Seed);
    }

    [Fact]
    public void Flat_black_photo_is_flat_ground_at_min()
    {
        var m = FileMapGenerator.Generate(Solid(64, 0x000000FF), S);
        Assert.All(m.Height, h => Assert.Equal(8, h));
        Assert.DoesNotContain(true, m.Water);
    }

    [Fact]
    public void Flat_white_photo_is_flat() =>
        Assert.All(FileMapGenerator.Generate(Solid(64, 0xFFFFFFFF), S).Height, h => Assert.Equal(16, h));

    [Fact]
    public void Textured_area_is_higher_than_flat_area()
    {
        var px = new uint[192 * 192];
        for (int y = 0; y < 192; y++)
            for (int x = 0; x < 192; x++)
                px[y * 192 + x] = x < 96 ? 0x808080FF : ((x + y) % 2 == 0 ? 0x202020FFu : 0xE0E0E0FFu);
        var m = FileMapGenerator.Generate(new PixelImage(192, 192, px), S);
        Assert.True(m.Height[m.Index(144, 96)] > m.Height[m.Index(48, 96)]);
    }

    [Fact]
    public void Flat_blue_area_becomes_water()
    {
        var m = FileMapGenerator.Generate(Solid(64, PixelImage.Pack(40, 90, 200)), S);
        Assert.All(m.Water, Assert.True);
        Assert.All(m.Height, h => Assert.Equal(6, h));
        Assert.Equal(8, m.WaterLevel);
    }

    [Fact]
    public void Heights_stay_in_bounds()
    {
        var rng = new Random(1234);
        var px = new uint[300 * 200];
        for (int i = 0; i < px.Length; i++) px[i] = PixelImage.Pack(rng.Next(256), rng.Next(256), rng.Next(256), rng.Next(256));
        var m = FileMapGenerator.Generate(new PixelImage(300, 200, px), S);
        for (int i = 0; i < m.Height.Length; i++)
        {
            if (m.Water[i]) Assert.Equal(6, m.Height[i]);
            else Assert.InRange(m.Height[i], 8, 32);
        }
    }

    [Fact]
    public void Stand_height_covers_the_whole_footprint()
    {
        // Final review: spawning on the centre column alone put the 0.3 m body inside a taller neighbour.
        var h = Enumerable.Repeat(8, 5 * 5).ToArray();
        h[1 * 5 + 2] = 13;             // column next to the centre (2,2) is 5 blocks taller
        var w = new bool[25];
        w[3 * 5 + 3] = true;           // a water column: standing height is its water level, not its floor
        var m = new FileMap(5, 8, 0, h, new uint[25], w);
        Assert.Equal(13, m.StandHeight(2, 2));
        Assert.Equal(8, m.StandHeight(4, 4));
        Assert.Equal(8, m.StandHeight(0, 4)); // edge cells clamp, no exception
    }

    [Fact]
    public void Color_is_the_prepared_photo_rgb()
    {
        var m = FileMapGenerator.Generate(Solid(64, 0xC86432FF), S);
        Assert.Equal(0xC86432u, m.Color[m.Index(10, 20)]);
    }
}
