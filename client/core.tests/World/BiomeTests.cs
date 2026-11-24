using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class BiomeTests
{
    internal static PixelImage Solid(int size, int r, int g, int b) =>
        new(size, size, Enumerable.Repeat(PixelImage.Pack(r, g, b), size * size).ToArray());

    /// <summary>Top third one color, the rest another (rows 0..size/3-1 are the top).</summary>
    internal static PixelImage TopAndBottom(int size, (int R, int G, int B) top, (int R, int G, int B) rest)
    {
        var px = new uint[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var (r, g, b) = y < size / 3 ? top : rest;
                px[y * size + x] = PixelImage.Pack(r, g, b);
            }
        return new PixelImage(size, size, px);
    }

    [Theory]
    [InlineData("/c/coffee_shop", Biome.Indoor)] [InlineData("/l/living_room", Biome.Indoor)] [InlineData("/r/restaurant", Biome.Indoor)]
    [InlineData("/h/highway", Biome.Road)] [InlineData("/s/street", Biome.Road)] [InlineData("/a/alley", Biome.Road)]
    [InlineData("/b/beach", Biome.Water)] [InlineData("/s/swimming_pool/outdoor", Biome.Water)]
    [InlineData("/f/forest_path", Biome.Forest)] [InlineData("/p/park", Biome.Forest)]
    [InlineData("/d/downtown", Biome.City)] [InlineData("/s/skyscraper", Biome.City)]
    [InlineData("/s/subway_station/platform", Biome.Transit)] [InlineData("/b/bus_interior", Biome.Transit)] [InlineData("/t/train_station/platform", Biome.Transit)]
    [InlineData("/p/pasture", Biome.Field)]
    public void Places_labels_map_to_biomes(string label, Biome b) => Assert.Equal(b, BiomeLabels.FromPlacesLabel(label));

    [Fact]
    public void Unknown_label_is_null() => Assert.Null(BiomeLabels.FromPlacesLabel("/x/not_a_place"));

    [Fact]
    public void Ambiguous_label_is_left_to_colors() => Assert.Null(BiomeLabels.FromPlacesLabel("/c/church/indoor"));

    [Fact]
    public void Every_biome_has_labels()
    {
        var counts = BiomeLabels.Table.Values.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(7, counts.Count);
        Assert.All(Enum.GetValues<Biome>(), b => Assert.True(counts[b] >= 10, $"{b} has {counts[b]} labels"));
    }

    [Fact]
    public void Blue_photo_guesses_water() => Assert.Equal(Biome.Water, ColorGuess.Guess(Solid(192, 40, 90, 200)));

    [Fact]
    public void Gray_photo_guesses_city() => Assert.Equal(Biome.City, ColorGuess.Guess(Solid(192, 120, 120, 125)));

    [Fact]
    public void Green_photo_guesses_forest() => Assert.Equal(Biome.Forest, ColorGuess.Guess(Solid(192, 50, 110, 45)));

    [Fact]
    public void Sky_over_grass_guesses_field() =>
        Assert.Equal(Biome.Field, ColorGuess.Guess(TopAndBottom(192, (150, 190, 240), (80, 150, 60))));

    [Fact]
    public void Photo_without_a_dominant_color_guesses_field()
    {
        // four vertical stripes: red, yellow, purple, brown - no rule reaches its share
        uint[] stripes = { PixelImage.Pack(200, 40, 40), PixelImage.Pack(230, 200, 60), PixelImage.Pack(120, 50, 140), PixelImage.Pack(110, 70, 40) };
        var px = new uint[192 * 192];
        for (int i = 0; i < px.Length; i++) px[i] = stripes[i % 192 / 48];
        Assert.Equal(Biome.Field, ColorGuess.Guess(new PixelImage(192, 192, px)));
    }

    [Fact]
    public void One_pixel_photo_does_not_throw() => Assert.Equal(Biome.Water, ColorGuess.Guess(Solid(1, 40, 90, 200)));
}
