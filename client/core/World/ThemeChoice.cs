using System.Text.Json;

namespace Hangbar.Core.World;

/// <summary>A photo's theme. Source is "places", "clip" (the two model candidates) or "color" (the color guess).</summary>
public sealed record ThemeResult(Biome Biome, float Confidence, string Source);

public static class ThemeChoice
{
    /// <summary>
    /// The model's answer when it is sure enough, otherwise the color guess (Confidence 0, Source "color").
    /// Never throws: a missing model, an unsure model and an odd photo all end in a biome (plan Review Focus 1).
    /// </summary>
    public static ThemeResult Choose(ThemeResult? model, PixelImage prepared, float minConfidence = 0.35f) =>
        model is { } m && m.Confidence >= minConfidence ? m : new ThemeResult(ColorGuess.Guess(prepared), 0f, "color");
}

/// <summary>
/// Remembers each photo's theme by world seed so the same photo never runs the model twice (plan Review Focus 2).
/// One JSON file: {"0123456789abcdef": {"biome": "Water", "confidence": 0.8, "source": "clip"}}. Keys are seeds,
/// never photo names or paths. A missing or unreadable file counts as empty.
/// </summary>
public sealed class ThemeCache(string path)
{
    sealed record Entry(string Biome, float Confidence, string Source);

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly Dictionary<string, Entry> _entries = Read(path);

    public ThemeResult? Get(ulong seed) =>
        _entries.TryGetValue(Key(seed), out var e) && Enum.TryParse<Biome>(e.Biome, out var biome)
            ? new ThemeResult(biome, e.Confidence, e.Source)
            : null;

    public void Put(ulong seed, ThemeResult r)
    {
        _entries[Key(seed)] = new Entry(r.Biome.ToString(), r.Confidence, r.Source);
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Json));
        File.Move(tmp, path, overwrite: true);
    }

    static string Key(ulong seed) => seed.ToString("x16");

    static Dictionary<string, Entry> Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }
}
