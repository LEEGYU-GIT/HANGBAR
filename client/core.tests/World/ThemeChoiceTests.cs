using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class ThemeChoiceTests
{
    static readonly PixelImage Blue = BiomeTests.Solid(192, 40, 90, 200);
    static readonly PixelImage Gray = BiomeTests.Solid(192, 120, 120, 125);

    [Fact]
    public void Low_confidence_falls_back_to_colors()
    {
        var r = ThemeChoice.Choose(new ThemeResult(Biome.Road, 0.2f, "places"), Blue);
        Assert.Equal(Biome.Water, r.Biome);
        Assert.Equal("color", r.Source);
        Assert.Equal(0f, r.Confidence);
    }

    [Fact]
    public void Missing_model_result_falls_back_to_colors() =>
        Assert.Equal(new ThemeResult(Biome.City, 0f, "color"), ThemeChoice.Choose(null, Gray));

    [Fact]
    public void Confident_model_wins() =>
        Assert.Equal(new ThemeResult(Biome.Indoor, 0.8f, "clip"), ThemeChoice.Choose(new ThemeResult(Biome.Indoor, 0.8f, "clip"), Blue));

    [Fact]
    public void Confidence_at_the_threshold_is_enough() =>
        Assert.Equal(Biome.Indoor, ThemeChoice.Choose(new ThemeResult(Biome.Indoor, 0.35f, "places"), Blue).Biome);

    [Fact]
    public void ThemeCache_roundtrips_and_survives_a_missing_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "inpc-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "theme-cache.json");
        try
        {
            Assert.Null(new ThemeCache(path).Get(0x0123456789ABCDEF));

            new ThemeCache(path).Put(0x0123456789ABCDEF, new ThemeResult(Biome.Transit, 0.62f, "clip"));
            var again = new ThemeCache(path);
            Assert.Equal(new ThemeResult(Biome.Transit, 0.62f, "clip"), again.Get(0x0123456789ABCDEF));
            Assert.Null(again.Get(42));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeCache_keeps_earlier_entries_when_adding()
    {
        string path = Path.Combine(Path.GetTempPath(), "inpc-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new ThemeCache(path).Put(1, new ThemeResult(Biome.Water, 0.9f, "places"));
            new ThemeCache(path).Put(2, new ThemeResult(Biome.Forest, 0f, "color"));
            var c = new ThemeCache(path);
            Assert.Equal(Biome.Water, c.Get(1)?.Biome);
            Assert.Equal(Biome.Forest, c.Get(2)?.Biome);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ThemeCache_treats_a_corrupt_file_as_empty()
    {
        string path = Path.Combine(Path.GetTempPath(), "inpc-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");
            var c = new ThemeCache(path);
            Assert.Null(c.Get(1));
            c.Put(1, new ThemeResult(Biome.City, 0.5f, "places"));
            Assert.Equal(Biome.City, new ThemeCache(path).Get(1)?.Biome);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
