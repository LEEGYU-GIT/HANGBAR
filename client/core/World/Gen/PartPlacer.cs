
namespace Hangbar.Core.World.Gen;

/// <summary>Turn seen from above, clockwise (east turns to south).</summary>
public enum Rotation { R0, R90, R180, R270 }

/// <param name="StretchTo">New length along the part's stretch axis (its marked range repeats).</param>
/// <param name="Missing">Share of solid cells left out at random (ruins).</param>
/// <param name="State">Wear given to every placed solid block.</param>
public sealed record PlaceOptions(int? StretchTo = null, double Missing = 0, BlockState State = BlockState.Intact);

/// <summary>Where a part ended up: inclusive world box and its anchors in world coordinates.</summary>
public sealed record PlacedPart(string Name, BlockPos Min, BlockPos Max, IReadOnlyDictionary<string, BlockPos> Anchors);

public static class PartPlacer
{
    public static PlacedPart Place(VoxelWorld w, Part part, BlockPos origin, Rotation rotation, PaletteRoles colors, PlaceOptions options, Rng rng)
    {
        int width = part.Width, depth = part.Depth;
        if (options.StretchTo is { } len && part.Stretch is { } s)
        {
            if (s.Axis == 'x') width = len; else depth = len;
        }
        int SourceX(int x) => part.Stretch is { Axis: 'x' } sx && width != part.Width ? Source(x, width, part.Width, sx.From, sx.To) : x;
        int SourceZ(int z) => part.Stretch is { Axis: 'z' } sz && depth != part.Depth ? Source(z, depth, part.Depth, sz.From, sz.To) : z;

        for (int y = 0; y < part.Height; y++)
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    if (part.CellAt(SourceX(x), y, SourceZ(z)) is not { } cell) continue;
                    if (cell.Material != Material.Air && options.Missing > 0 && rng.Next() < options.Missing) continue;
                    var (rx, rz) = Turn(x, z, width, depth, rotation);
                    w.Set(origin.X + rx, origin.Y + y, origin.Z + rz, BlockOf(cell, colors, options.State));
                }

        var anchors = new Dictionary<string, BlockPos>();
        foreach (var (name, a) in part.Anchors)
        {
            var (ax, az) = Turn(a.X, a.Z, width, depth, rotation);
            anchors[name] = new BlockPos(origin.X + ax, origin.Y + a.Y, origin.Z + az);
        }
        bool sideways = rotation is Rotation.R90 or Rotation.R270;
        int footX = sideways ? depth : width, footZ = sideways ? width : depth;
        return new PlacedPart(part.Name, origin, new BlockPos(origin.X + footX - 1, origin.Y + part.Height - 1, origin.Z + footZ - 1), anchors);
    }

    /// <summary>Local (x, z) inside a width×depth footprint → offset after turning, still inside the turned footprint.</summary>
    static (int X, int Z) Turn(int x, int z, int width, int depth, Rotation r) => r switch
    {
        Rotation.R90 => (depth - 1 - z, x),
        Rotation.R180 => (width - 1 - x, depth - 1 - z),
        Rotation.R270 => (z, width - 1 - x),
        _ => (x, z),
    };

    /// <summary>Index in the original part for index i of a stretched length: ends stay, the marked range repeats.</summary>
    static int Source(int i, int length, int original, int from, int to)
    {
        int before = from, after = original - 1 - to, span = to - from + 1;
        if (i < before) return i;
        if (i >= length - after) return original - (length - i);
        return from + (i - before) % span;
    }

    static uint BlockOf(PartCell cell, PaletteRoles colors, BlockState state)
    {
        if (cell.Material == Material.Air) return Block.Air;
        uint rgb = cell.Role is { } role ? colors[role] : cell.FixedColor;
        if (cell.Material == Material.Water) return Block.Make(Material.Water, rgb);
        return Block.Solid(rgb, cell.Material, state);
    }
}

/// <summary>Every part shipped as an embedded resource "parts/NAME.part".</summary>
public static class PartLibrary
{
    static readonly Lazy<Dictionary<string, Part>> Parts = new(Load);

    public static IReadOnlyList<string> Names => Parts.Value.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

    public static Part Get(string name) =>
        Parts.Value.TryGetValue(name, out var p) ? p : throw new KeyNotFoundException($"no part named '{name}'");

    static Dictionary<string, Part> Load()
    {
        var asm = typeof(PartLibrary).Assembly;
        var parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        foreach (string res in asm.GetManifestResourceNames().Where(r => r.StartsWith("parts/") && r.EndsWith(".part")))
        {
            string name = res["parts/".Length..^".part".Length];
            using var stream = asm.GetManifestResourceStream(res)!;
            using var reader = new StreamReader(stream);
            parts[name] = Part.Parse(reader.ReadToEnd(), name);
        }
        return parts;
    }
}
