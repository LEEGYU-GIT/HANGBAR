using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class SceneTests
{
    [Fact]
    public void Nineteen_scene_variants_and_part_one_keeps_its_numbers()
    {
        Assert.Equal(19, Enum.GetValues<Scene>().Length);
        Assert.Equal((0, 1, 2, 3), ((int)Scene.Beach, (int)Scene.Pool, (int)Scene.House, (int)Scene.Mountain));
    }

    [Fact]
    public void Every_biome_has_its_scenes()
    {
        var counts = Enum.GetValues<Scene>().GroupBy(Scenes.BiomeOf).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, counts[Biome.Field]);
        Assert.Equal(3, counts[Biome.Road]);
        Assert.Equal(3, counts[Biome.Water]);
        Assert.Equal(3, counts[Biome.Forest]);
        Assert.Equal(2, counts[Biome.City]);
        Assert.Equal(4, counts[Biome.Indoor]);
        Assert.Equal(2, counts[Biome.Transit]);
    }

    [Theory]
    [InlineData(Biome.Field, Scene.Meadow)] [InlineData(Biome.Road, Scene.CountryRoad)] [InlineData(Biome.Water, Scene.Beach)]
    [InlineData(Biome.Forest, Scene.Forest)] [InlineData(Biome.City, Scene.Downtown)] [InlineData(Biome.Indoor, Scene.House)]
    [InlineData(Biome.Transit, Scene.SubwayStation)]
    public void Default_scenes_follow_the_design(Biome b, Scene s)
    {
        Assert.Equal(s, Scenes.DefaultFor(b));
        Assert.Equal(b, Scenes.BiomeOf(s));
    }

    [Fact]
    public void Only_the_part_one_scenes_are_built()
    {
        var built = Enum.GetValues<Scene>().Where(Scenes.IsBuilt).ToHashSet();
        Assert.True(built.SetEquals(new[] { Scene.Beach, Scene.Pool, Scene.House, Scene.Mountain }), string.Join(",", built));
    }
}
