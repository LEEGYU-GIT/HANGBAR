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
        Assert.Equal(new ThemeResult(Biome.City, 0f, "color", Scene.Downtown), ThemeChoice.Choose(null, Gray)); // the biome's default scene

    [Fact]
    public void Confident_model_wins() =>
        Assert.Equal(new ThemeResult(Biome.Indoor, 0.8f, "clip", Scene.Cafe), ThemeChoice.Choose(new ThemeResult(Biome.Indoor, 0.8f, "clip", Scene.Cafe), Blue));

    [Fact]
    public void Model_without_a_scene_gets_the_biome_default() =>
        Assert.Equal(Scene.House, ThemeChoice.Choose(new ThemeResult(Biome.Indoor, 0.8f, "clip"), Blue).Scene);

    [Fact]
    public void Scene_outside_the_biome_gets_the_biome_default() =>
        Assert.Equal(Scene.Beach, ThemeChoice.Choose(new ThemeResult(Biome.Water, 0.8f, "places", Scene.House), Blue).Scene);

    [Fact]
    public void ThemeCache_keeps_the_scene_and_reads_old_entries()
    {
        string path = Path.Combine(Path.GetTempPath(), "inpc-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new ThemeCache(path).Put(1, new ThemeResult(Biome.Water, 0.9f, "places", Scene.Pool));
            Assert.Equal(Scene.Pool, new ThemeCache(path).Get(1)?.Scene);
            // an entry written before scenes existed: the biome's default scene
            File.WriteAllText(path, "{\"0000000000000002\": {\"biome\": \"Indoor\", \"confidence\": 0.7, \"source\": \"clip\"}}");
            Assert.Equal(new ThemeResult(Biome.Indoor, 0.7f, "clip", Scene.House), new ThemeCache(path).Get(2));
        }
        finally
        {
            File.Delete(path);
        }
    }

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

            new ThemeCache(path).Put(0x0123456789ABCDEF, new ThemeResult(Biome.Transit, 0.62f, "clip", Scene.TrainStation));
            var again = new ThemeCache(path);
            Assert.Equal(new ThemeResult(Biome.Transit, 0.62f, "clip", Scene.TrainStation), again.Get(0x0123456789ABCDEF));
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
    public void Undefined_model_biome_falls_back_to_colors() =>
        Assert.Equal(new ThemeResult(Biome.Water, 0f, "color", Scene.Beach), ThemeChoice.Choose(new ThemeResult((Biome)9, 0.9f, "clip"), Blue));

    [Fact]
    public void ThemeCache_ignores_entries_it_cannot_use()
    {
        string path = Path.Combine(Path.GetTempPath(), "inpc-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """
                {"0000000000000001": null,
                 "0000000000000002": {"biome": "9", "confidence": 0.5, "source": "clip"},
                 "0000000000000003": {"biome": "Water", "confidence": 0.5, "source": null},
                 "0000000000000004": {"biome": "Road", "confidence": 0.7, "source": "places"}}
                """);
            var c = new ThemeCache(path);
            Assert.Null(c.Get(1));
            Assert.Null(c.Get(2));
            Assert.Null(c.Get(3));
            Assert.Equal(new ThemeResult(Biome.Road, 0.7f, "places", Scene.CountryRoad), c.Get(4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ThemeCache_keeps_working_when_it_cannot_write()
    {
        string blocker = Path.GetTempFileName(); // a file where the cache's folder would have to be
        try
        {
            var c = new ThemeCache(Path.Combine(blocker, "theme-cache.json"));
            c.Put(5, new ThemeResult(Biome.Forest, 0.6f, "clip"));
            Assert.Equal(Biome.Forest, c.Get(5)?.Biome);
        }
        finally
        {
            File.Delete(blocker);
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
