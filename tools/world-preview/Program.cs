using Hangbar.Core.World;
using Hangbar.Core.World.Gen;
using WorldPreview;

// world-preview: pictures of the v2 generator's worlds for checking without the game.
//   dotnet run --project tools/world-preview -- --scene=mountain|beach|pool|house|all [--photo=photo.ppm] [--seed=N] --out=FOLDER
// Prints one stats line per scene; never prints the photo's name or path.

var args2 = args.Where(a => a.StartsWith("--") && a.Contains('=')).ToDictionary(a => a[2..a.IndexOf('=')], a => a[(a.IndexOf('=') + 1)..]);
if (!args2.TryGetValue("out", out var outDir) || !args2.TryGetValue("scene", out var sceneArg))
{
    Console.Error.WriteLine("usage: world-preview --scene=mountain|beach|pool|house|all [--photo=photo.ppm] [--seed=N] --out=FOLDER");
    return 2;
}

PixelImage photo = SamplePhotos.Meadow();
if (args2.TryGetValue("photo", out var photoPath))
{
    string photoDir = Path.GetDirectoryName(Path.GetFullPath(photoPath)) ?? "";
    if (string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(outDir)), Path.TrimEndingDirectorySeparator(photoDir), StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("the output folder is the photo's folder; choose another folder");
        return 2;
    }
    photo = Ppm.Read(photoPath) ?? throw new InvalidDataException("the photo is not a binary PPM (P6) file");
}
ulong seedOverride = args2.TryGetValue("seed", out var s) && ulong.TryParse(s, out var sv) ? sv : 0;

var prepared = ImageFit.Prepare(photo, 192);
ulong seed = seedOverride != 0 ? seedOverride : WorldSeed.Of(prepared);
var scenes = sceneArg == "all" ? Enum.GetValues<Scene>() : new[] { Scenes.FromName(sceneArg) ?? throw new ArgumentException($"unknown scene '{sceneArg}'") };
Directory.CreateDirectory(outDir);

foreach (var scene in scenes)
{
    GeneratedWorld g;
    try
    {
        g = WorldGenerator.Generate(scene, Palette.Extract(prepared), MoodReader.Read(prepared), prepared, seed, Horizon.Estimate(prepared));
    }
    catch (NotSupportedException)
    {
        Console.WriteLine($"scene={scene.ToString().ToLowerInvariant()} skipped: not built yet");
        continue;
    }
    string name = scene.ToString().ToLowerInvariant();
    Render.Iso(g).Save(Path.Combine(outDir, $"{name}-iso.png"));
    Render.Top(g).Save(Path.Combine(outDir, $"{name}-top.png"));
    Render.Slice(g).Save(Path.Combine(outDir, $"{name}-slice.png"));
    Render.Close(g).Save(Path.Combine(outDir, $"{name}-close.png"));

    int tris = 0;
    for (int cy = 0; cy < g.World.ChunksY; cy++)
        for (int cz = 0; cz < g.World.ChunksZ; cz++)
            for (int cx = 0; cx < g.World.ChunksX; cx++)
            {
                var m = ChunkMesher.Build(g.World, cx, cy, cz);
                tris += m.Opaque.TriangleCount + m.Glass.TriangleCount + m.Water.TriangleCount;
            }
    int n = g.World.SizeX;
    double walk = Walkability.ReachColumns(g.World, g.Spawn).Count(b => b) * 100.0 / (n * n);
    double water = g.Surface.Water.Count(b => b) * 100.0 / (n * n);
    var mu = g.Mural;
    Console.WriteLine($"scene={name} seed={seed} tris={tris} walk={walk:0}% water={water:0}% " +
                      $"mural=({mu.Origin.X},{mu.Origin.Y},{mu.Origin.Z} {mu.Facing.ToString().ToLowerInvariant()} {mu.Width}x{mu.Height}) landmark={g.Landmark.Name}");
}
return 0;

/// <summary>Binary PPM (P6, 8-bit) reader: lets the tool take a photo without an image library.</summary>
static class Ppm
{
    public static PixelImage? Read(string path)
    {
        byte[] b;
        try { b = File.ReadAllBytes(path); }
        catch (Exception) { return null; }
        int pos = 0;
        string Token()
        {
            while (pos < b.Length && (char.IsWhiteSpace((char)b[pos]) || b[pos] == '#'))
            {
                if (b[pos] == '#') while (pos < b.Length && b[pos] != '\n') pos++;
                else pos++;
            }
            int start = pos;
            while (pos < b.Length && !char.IsWhiteSpace((char)b[pos])) pos++;
            return System.Text.Encoding.ASCII.GetString(b, start, pos - start);
        }
        if (Token() != "P6") return null;
        if (!int.TryParse(Token(), out int w) || !int.TryParse(Token(), out int h) || Token() != "255") return null;
        pos++; // the single whitespace after the header
        if (b.Length - pos < w * h * 3) return null;
        var px = new uint[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = PixelImage.Pack(b[pos + i * 3], b[pos + i * 3 + 1], b[pos + i * 3 + 2]);
        return new PixelImage(w, h, px);
    }
}
