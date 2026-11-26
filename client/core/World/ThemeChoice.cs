using System.Text.Json;

namespace Hangbar.Core.World;

/// <summary>
/// A photo's theme. Source is "places", "clip" (the two model candidates) or "color" (the color guess).
/// Scene is the variant inside the biome (v2 design §6-1); a result from <see cref="ThemeChoice"/> always has one.
/// </summary>
public sealed record ThemeResult(Biome Biome, float Confidence, string Source, Scene? Scene = null);

public static class ThemeChoice
{
    /// <summary>
    /// The model's answer when it is sure enough, otherwise the color guess (Confidence 0, Source "color").
    /// Never throws: a missing model, an unsure model and an odd photo all end in a biome (plan Review Focus 1).
    /// </summary>
    /// The scene is the model's when it lies in the chosen biome, otherwise the biome's default scene.
    public static ThemeResult Choose(ThemeResult? model, PixelImage prepared, float minConfidence = 0.35f) =>
        WithScene(model is { } m && Enum.IsDefined(m.Biome) && m.Confidence >= minConfidence
            ? m
            : new ThemeResult(ColorGuess.Guess(prepared), 0f, "color"));

    /// <summary>The result with a scene that lies in its biome (the biome's default when it has none or a wrong one).</summary>
    public static ThemeResult WithScene(ThemeResult r) =>
        r.Scene is { } s && Enum.IsDefined(s) && Scenes.BiomeOf(s) == r.Biome ? r : r with { Scene = Scenes.DefaultFor(r.Biome) };
}

/// <summary>
/// Remembers each photo's theme by world seed so the same photo never runs the model twice (plan Review Focus 2).
/// One JSON file: {"0123456789abcdef": {"biome": "Water", "confidence": 0.8, "source": "clip"}}. Keys are seeds,
/// never photo names or paths. A missing or unreadable file counts as empty, an entry it cannot use counts as
/// missing, and a failed write keeps the answer in memory: the cache never stops the game.
/// Put what a model answered; a color guess made because no model was available is better not cached, so the
/// photo is recognized once the model is back.
/// </summary>
public sealed class ThemeCache(string path)
{
    sealed record Entry(string? Biome, float Confidence, string? Source, string? Scene = null);

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly Dictionary<string, Entry?> _entries = Read(path);

    public ThemeResult? Get(ulong seed) =>
        _entries.TryGetValue(Key(seed), out var e) && e is { Source: { } source }
        && Enum.TryParse<Biome>(e.Biome, out var biome) && Enum.IsDefined(biome)
            ? ThemeChoice.WithScene(new ThemeResult(biome, e.Confidence, source,
                Enum.TryParse<Scene>(e.Scene, out var scene) && Enum.IsDefined(scene) ? scene : null))
            : null;

    public void Put(ulong seed, ThemeResult r)
    {
        _entries[Key(seed)] = new Entry(r.Biome.ToString(), r.Confidence, r.Source, r.Scene?.ToString());
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // keep the answer for this session; the next Put tries the file again
        }
    }

    static string Key(ulong seed) => seed.ToString("x16");

    static Dictionary<string, Entry?> Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Entry?>>(File.ReadAllText(path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }
}
