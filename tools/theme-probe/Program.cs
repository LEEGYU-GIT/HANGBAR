using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Hangbar.Core.World;
using Hangbar.Theme;

// theme-probe: runs both theme model candidates over a folder of photos and prints a table (G0.5 Task 4, Task 7).
//   dotnet run --project tools/theme-probe -- --photos=FOLDER [--models=FOLDER]
// Put photos in subfolders named after the scene they show (beach, pool, house, mountain, cafe, ...) to get
// accuracy; other photos are just classified. Photos are numbered: names and paths are never printed (design §3).

var opts = args.Where(a => a.StartsWith("--") && a.Contains('=')).ToDictionary(a => a[2..a.IndexOf('=')], a => a[(a.IndexOf('=') + 1)..]);
if (!opts.TryGetValue("photos", out var photosDir) || !Directory.Exists(photosDir))
{
    Console.Error.WriteLine("usage: theme-probe --photos=FOLDER [--models=FOLDER]   (subfolders named after scenes give accuracy)");
    return 2;
}
string models = opts.GetValueOrDefault("models", Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "models"));

var candidates = new List<IThemeModel>();
try { candidates.Add(new PlacesModel(Path.Combine(models, "places365.onnx"), Path.Combine(models, "places365-labels.txt"))); }
catch (Exception e) { Console.WriteLine($"places: not available ({e.GetType().Name}); see models/README.md"); }
try { candidates.Add(new ClipModel(Path.Combine(models, "clip-vision.onnx"), Path.Combine(models, "clip-text-scenes.json"))); }
catch (Exception e) { Console.WriteLine($"clip: not available ({e.GetType().Name}); see models/README.md"); }

var files = Directory.EnumerateFiles(photosDir, "*", SearchOption.AllDirectories)
    .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp")
    .OrderBy(f => f, StringComparer.Ordinal).ToList();
Console.WriteLine($"{files.Count} photos, {candidates.Count} models");

var rows = new List<(Scene? Truth, Dictionary<string, (ThemeResult? Raw, ThemeResult Final, long Ms)> Got)>();
int n = 0;
foreach (var file in files)
{
    n++;
    var photo = Load(file);
    if (photo is null) { Console.WriteLine($"photo {n}: unreadable"); continue; }
    string parent = Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
    Scene? truth = Path.GetFullPath(Path.GetDirectoryName(file)!) == Path.GetFullPath(photosDir) ? null : Scenes.FromName(parent);
    var prepared = ImageFit.Prepare(photo, 192);
    var got = new Dictionary<string, (ThemeResult?, ThemeResult, long)>();
    foreach (var m in candidates)
    {
        var sw = Stopwatch.StartNew();
        var raw = m.Classify(photo);
        long ms = sw.ElapsedMilliseconds;
        got[m.Name] = (raw, ThemeChoice.Choose(raw, prepared), ms);
    }
    rows.Add((truth, got));
    string Cell(string name) => got.TryGetValue(name, out var g)
        ? $"{g.Item2.Biome,-8}{g.Item2.Scene,-14}{(g.Item1?.Confidence ?? 0),5:0.00}{(g.Item2.Source == "color" ? " (color)" : ""),-8}{g.Item3,5}ms"
        : "-";
    Console.WriteLine($"photo {n,3}  truth {truth?.ToString() ?? "?",-14} | places {Cell("places")} | clip {Cell("clip")}");
}

Console.WriteLine();
foreach (var m in candidates)
{
    var judged = rows.Where(r => r.Truth is not null && r.Got.ContainsKey(m.Name)).ToList();
    var times = rows.Where(r => r.Got.ContainsKey(m.Name)).Select(r => r.Got[m.Name].Ms).Skip(1).DefaultIfEmpty(0).ToList();
    if (judged.Count == 0) { Console.WriteLine($"{m.Name}: no photos in scene-named subfolders; avg {times.Average():0} ms"); continue; }
    double biome = judged.Count(r => r.Got[m.Name].Final.Biome == Scenes.BiomeOf(r.Truth!.Value)) / (double)judged.Count;
    double scene = judged.Count(r => r.Got[m.Name].Final.Scene == r.Truth) / (double)judged.Count;
    Console.WriteLine($"{m.Name}: {judged.Count} judged — biome {biome:P0}, scene {scene:P0} (pass: scene ≥ 70%){(scene >= 0.7 ? " PASS" : "")}; " +
                      $"avg {times.Average():0} ms, max {times.Max()} ms (limit 1000)");
}
foreach (var m in candidates) m.Dispose();
return 0;

static PixelImage? Load(string path)
{
    try
    {
        using var bmp = new Bitmap(path);
        var px = new uint[bmp.Width * bmp.Height];
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[data.Stride * bmp.Height];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bmp.UnlockBits(data);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                int o = y * data.Stride + x * 4; // BGRA
                px[y * bmp.Width + x] = PixelImage.Pack(bytes[o + 2], bytes[o + 1], bytes[o], bytes[o + 3]);
            }
        return new PixelImage(bmp.Width, bmp.Height, px);
    }
    catch (Exception)
    {
        return null;
    }
}
