using System.Text.Json;
using Hangbar.Core.World;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Hangbar.Theme;

/// <summary>
/// A local model that names a photo's theme: biome and scene variant (design §6-3-1, v2 design §6-1). Runs on the
/// CPU; the photo never leaves the PC. Classify returns null when the model cannot place the photo at all; the caller
/// then goes to <see cref="ThemeChoice"/> and the color guess.
/// </summary>
public interface IThemeModel : IDisposable
{
    string Name { get; }
    ThemeResult? Classify(PixelImage photo);
}

/// <summary>Shared helpers: score sums per biome and scene, the pick, file checks.</summary>
static class Pick
{
    /// <summary>The biome with the largest summed probability, then that biome's most likely scene.</summary>
    public static ThemeResult? Best(IReadOnlyDictionary<Scene, float> sceneScores, string source)
    {
        if (sceneScores.Count == 0) return null;
        var byBiome = sceneScores.GroupBy(kv => Scenes.BiomeOf(kv.Key)).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
        var biome = byBiome.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First();
        var scene = sceneScores.Where(kv => Scenes.BiomeOf(kv.Key) == biome.Key).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        return new ThemeResult(biome.Key, biome.Value, source, scene);
    }

    public static float[] Softmax(ReadOnlySpan<float> x, float scale = 1f)
    {
        float max = float.MinValue;
        foreach (float v in x) max = Math.Max(max, v * scale);
        var e = new float[x.Length];
        double sum = 0;
        for (int i = 0; i < x.Length; i++) sum += e[i] = MathF.Exp(x[i] * scale - max);
        for (int i = 0; i < e.Length; i++) e[i] = (float)(e[i] / sum);
        return e;
    }

    public static void Require(params string[] paths)
    {
        foreach (var p in paths)
            if (!File.Exists(p)) throw new FileNotFoundException("a theme model file is missing (see models/README.md)");
    }
}

/// <summary>
/// Candidate A: Places365 ResNet18 scene classifier. The top five of its 365 places are mapped through
/// biome-labels.csv; their probabilities are added up per scene, the best biome wins and its best scene is named.
/// Confidence is the biome's summed probability. Null when none of the five places is in the table.
/// </summary>
public sealed class PlacesModel : IThemeModel
{
    static readonly float[] Mean = { 0.485f, 0.456f, 0.406f }, Std = { 0.229f, 0.224f, 0.225f };
    readonly InferenceSession _session;
    readonly string[] _labels;

    public string Name => "places";

    public PlacesModel(string onnxPath, string labelsPath)
    {
        Pick.Require(onnxPath, labelsPath);
        _labels = File.ReadAllLines(labelsPath).Where(l => l.Length > 0).ToArray();
        _session = new InferenceSession(onnxPath);
    }

    public ThemeResult? Classify(PixelImage photo)
    {
        var input = new DenseTensor<float>(Preprocess.ToChw(photo, 224, Mean, Std), new[] { 1, 3, 224, 224 });
        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", input) });
        var logits = results.First(r => r.Name == "logits").AsEnumerable<float>().ToArray();
        var p = Pick.Softmax(logits);
        var scores = new Dictionary<Scene, float>();
        foreach (int i in Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).Take(5))
            if (i < _labels.Length && BiomeLabels.SceneFromPlacesLabel(_labels[i]) is { } scene)
                scores[scene] = scores.GetValueOrDefault(scene) + p[i];
        return Pick.Best(scores, Name);
    }

    public void Dispose() => _session.Dispose();
}

/// <summary>
/// Candidate B: CLIP ViT-B/32 image encoder against precomputed sentence embeddings of the nineteen scenes
/// (clip-text-scenes.json, made by tools/theme-probe/make_text_embeddings.py). Cosine similarity × 100 → softmax over
/// the scenes; probabilities are added up per biome, the best biome wins and its best scene is named.
/// </summary>
public sealed class ClipModel : IThemeModel
{
    static readonly float[] Mean = { 0.4815f, 0.4578f, 0.4082f }, Std = { 0.2686f, 0.2613f, 0.2758f };
    readonly InferenceSession _session;
    readonly (Scene Scene, float[] Vector)[] _scenes;

    public string Name => "clip";

    public ClipModel(string visionOnnxPath, string sceneEmbeddingsJson)
    {
        Pick.Require(visionOnnxPath, sceneEmbeddingsJson);
        var json = JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(sceneEmbeddingsJson))
                   ?? throw new InvalidDataException("the scene embeddings file is empty");
        _scenes = json.Select(kv => (Scenes.FromName(kv.Key) ?? throw new InvalidDataException($"unknown scene '{kv.Key}' in embeddings"), kv.Value))
            .ToArray();
        _session = new InferenceSession(visionOnnxPath);
    }

    public ThemeResult? Classify(PixelImage photo)
    {
        var input = new DenseTensor<float>(Preprocess.ToChw(photo, 224, Mean, Std), new[] { 1, 3, 224, 224 });
        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("pixel_values", input) });
        var image = results.First(r => r.Name == "image_embeds").AsEnumerable<float>().ToArray();
        float norm = MathF.Sqrt(image.Sum(v => v * v));
        var cos = _scenes.Select(s => s.Vector.Zip(image, (a, b) => a * b).Sum() / norm).ToArray();
        var p = Pick.Softmax(cos, 100f);
        var scores = new Dictionary<Scene, float>();
        for (int i = 0; i < _scenes.Length; i++) scores[_scenes[i].Scene] = p[i];
        return Pick.Best(scores, Name);
    }

    public void Dispose() => _session.Dispose();
}
