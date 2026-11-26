using System.Reflection;

namespace Hangbar.Core.World;

/// <summary>The kind of world a photo becomes (design §6-3). Field is the default.</summary>
public enum Biome { Field, Road, Water, Forest, City, Indoor, Transit }

/// <summary>
/// Places365 scene labels → biome and scene variant, from the embedded table biome-labels.csv
/// (design §6-3-1, v2 design §6-1). Labels left out on purpose (ambiguous places) go to the color guess.
/// </summary>
public static class BiomeLabels
{
    static readonly (Dictionary<string, Biome> Biomes, Dictionary<string, Scene> Scenes) Tables = Load();

    /// <summary>Scene name without the "/x/" prefix and tail ("swimming_pool", not "/s/swimming_pool/outdoor").</summary>
    public static IReadOnlyDictionary<string, Biome> Table => Tables.Biomes;

    /// <summary>The same names → scene variant; every variant lies in the label's biome.</summary>
    public static IReadOnlyDictionary<string, Scene> SceneTable => Tables.Scenes;

    /// <summary>
    /// "/c/coffee_shop" → Indoor, "/s/swimming_pool/outdoor" → Water. Null for a label the table leaves out on
    /// purpose (ambiguous scenes go to the color guess) or does not know.
    /// </summary>
    public static Biome? FromPlacesLabel(string label)
    {
        return Table.TryGetValue(Name(label), out var biome) ? biome : null;
    }

    /// <summary>"/s/swimming_pool/outdoor" → Pool, "/c/coffee_shop" → Cafe; null as for the biome.</summary>
    public static Scene? SceneFromPlacesLabel(string label) => SceneTable.TryGetValue(Name(label), out var s) ? s : null;

    static string Name(string label)
    {
        var name = label.AsSpan().Trim();
        if (name.Length > 3 && name[0] == '/' && name[2] == '/') name = name[3..];
        int tail = name.IndexOf('/');
        if (tail >= 0) name = name[..tail];
        return name.ToString();
    }

    static (Dictionary<string, Biome>, Dictionary<string, Scene>) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("biome-labels.csv")
            ?? throw new InvalidOperationException("biome-labels.csv is not embedded");
        using var reader = new StreamReader(stream);
        var biomes = new Dictionary<string, Biome>(StringComparer.Ordinal);
        var scenes = new Dictionary<string, Scene>(StringComparer.Ordinal);
        reader.ReadLine(); // header: label,biome,scene
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var parts = line.Split(',');
            string name = parts[0].Trim();
            biomes.Add(name, Enum.Parse<Biome>(parts[1].Trim()));
            scenes.Add(name, Enum.Parse<Scene>(parts[2].Trim()));
        }
        return (biomes, scenes);
    }
}

/// <summary>
/// Biome from colors alone, for when no model is available or the model is unsure (design §6-3-1).
/// Rules in order: sky over grass → Field, ≥40% blue → Water, ≥50% green → Forest, ≥60% gray → City, else Field.
/// </summary>
public static class ColorGuess
{
    public static Biome Guess(PixelImage prepared)
    {
        int w = prepared.Width, h = prepared.Height, band = Math.Max(1, h / 3);
        if (Share(prepared, 0, band, IsSky) >= 0.5 && Share(prepared, h - band, h, IsGreen) >= 0.5) return Biome.Field;
        if (Share(prepared, 0, h, IsBlue) >= 0.4) return Biome.Water;
        if (Share(prepared, 0, h, IsGreen) >= 0.5) return Biome.Forest;
        if (Share(prepared, 0, h, IsGray) >= 0.6) return Biome.City;
        return Biome.Field;
    }

    static double Share(PixelImage img, int y0, int y1, Func<double, double, double, bool> test)
    {
        int hits = 0;
        for (int y = y0; y < y1; y++)
            for (int x = 0; x < img.Width; x++)
            {
                var (hue, sat, val) = Hsv(img[x, y]);
                if (test(hue, sat, val)) hits++;
            }
        return (double)hits / ((y1 - y0) * img.Width);
    }

    static bool IsBlue(double hue, double sat, double val) => hue is >= 185 and <= 255 && sat >= 0.25 && val >= 0.2;
    static bool IsSky(double hue, double sat, double val) => hue is >= 185 and <= 255 && sat >= 0.1 && val >= 0.6;
    static bool IsGreen(double hue, double sat, double val) => hue is >= 65 and <= 170 && sat >= 0.2 && val >= 0.15;
    static bool IsGray(double hue, double sat, double val) => sat < 0.15 && val is >= 0.12 and <= 0.92;

    /// <summary>Hue in degrees (0..360), saturation and value in 0..1.</summary>
    static (double Hue, double Sat, double Val) Hsv(uint rgba)
    {
        var (r, g, b, _) = PixelImage.Unpack(rgba);
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double hue = d == 0 ? 0
            : max == r ? 60.0 * ((g - b) / (double)d) + (g < b ? 360 : 0)
            : max == g ? 60.0 * ((b - r) / (double)d) + 120
            : 60.0 * ((r - g) / (double)d) + 240;
        return (hue, max == 0 ? 0 : d / (double)max, max / 255.0);
    }
}
