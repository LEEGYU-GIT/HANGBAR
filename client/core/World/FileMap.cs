namespace Hangbar.Core.World;

/// <summary>File map generation settings. All heights are in blocks (0.25 m).</summary>
public sealed record MapGenSettings(int Size = 192, int MinGround = 8, int Relief = 24, int WaterLevel = 8);

/// <summary>One photo turned into terrain columns: height, surface color and water (design §6-3).</summary>
public sealed class FileMap
{
    public int Size { get; }
    public int WaterLevel { get; }
    public ulong Seed { get; }
    /// <summary>Solid blocks per column; the top block sits at y = Height - 1.</summary>
    public int[] Height { get; }
    /// <summary>Surface color per column, 0xRRGGBB.</summary>
    public uint[] Color { get; }
    public bool[] Water { get; }

    public FileMap(int size, int waterLevel, ulong seed, int[] height, uint[] color, bool[] water)
    {
        Size = size; WaterLevel = waterLevel; Seed = seed; Height = height; Color = color; Water = water;
    }

    public int Index(int x, int z) => z * Size + x;

    /// <summary>
    /// Height (in blocks) a body standing on column (x, z) rests at: the highest ground or water surface over the
    /// 3×3 footprint, so a 0.3 m-radius body never starts inside a taller neighbour column.
    /// </summary>
    public int StandHeight(int x, int z)
    {
        int best = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int i = Index(Math.Clamp(x + dx, 0, Size - 1), Math.Clamp(z + dz, 0, Size - 1));
                best = Math.Max(best, Water[i] ? Math.Max(Height[i], WaterLevel) : Height[i]);
            }
        return best;
    }
}

/// <summary>Round props drawn by the game, not as blocks (design §4-2: only terrain and buildings are blocks).</summary>
public enum PropKind { Tree, Bush, Rock, Cup, Plant, Lamp, Bench, Parasol, Float }

/// <summary>
/// A prop standing on column (X, Z); the game puts it on the highest block there. Scale multiplies the prop's
/// normal size (indoor cups and plants are giants, ×4 and more). Color is 0xRRGGBB. Y ≥ 0 puts it on that block
/// height instead (a cup on a table, a float on the water surface).
/// </summary>
public sealed record Prop(PropKind Kind, int X, int Z, float Scale, uint Color, int Y = -1);

/// <summary>
/// A box of blocks over columns X..X+W-1, Z..Z+D-1 (city building, indoor furniture or step, platform, rail).
/// It stands on the highest ground under it and is Height blocks tall; with Lift it floats that many blocks above
/// the ground instead (the top of a tunnel arch).
/// </summary>
public sealed record Building(int X, int Z, int W, int D, int Height, uint Color, int Lift = 0);

/// <summary>A generated biome: terrain columns plus the buildings and props on them (design §6-3).</summary>
public sealed class BiomeMap(FileMap terrain, IReadOnlyList<Building> buildings, IReadOnlyList<Prop> props, Biome biome, Mood mood)
{
    public FileMap Terrain { get; } = terrain;
    public IReadOnlyList<Building> Buildings { get; } = buildings;
    public IReadOnlyList<Prop> Props { get; } = props;
    public Biome Biome { get; } = biome;
    public Mood Mood { get; } = mood;
}

/// <summary>World identity from photo content, never from file names or paths.</summary>
public static class WorldSeed
{
    /// <summary>FNV-1a 64 over the prepared image's pixels.</summary>
    public static ulong Of(PixelImage prepared)
    {
        ulong h = 14695981039346656037UL;
        foreach (uint p in prepared.Rgba)
            for (int s = 24; s >= 0; s -= 8)
            {
                h ^= (p >> s) & 0xFF;
                h *= 1099511628211UL;
            }
        return h;
    }
}

public static class FileMapGenerator
{
    /// <summary>
    /// Wide even areas become low plains, busy areas become hills: height comes mostly from local texture,
    /// a little from brightness. Flat blue areas become water.
    /// </summary>
    public static FileMap Generate(PixelImage photo, MapGenSettings s)
    {
        var img = ImageFit.Prepare(photo, s.Size);
        int n = s.Size;
        var lum = new double[n * n];
        for (int i = 0; i < lum.Length; i++)
        {
            var (r, g, b, _) = PixelImage.Unpack(img.Rgba[i]);
            lum[i] = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        }

        var detail = new double[n * n];
        var raw = new double[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                double sum = 0, sq = 0;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        double l = lum[Clamp(z + dz, n) * n + Clamp(x + dx, n)];
                        sum += l; sq += l * l;
                    }
                double mean = sum / 25, variance = Math.Max(0, sq / 25 - mean * mean);
                int i = z * n + x;
                detail[i] = Math.Clamp(Math.Sqrt(variance) / 0.2, 0, 1);
                raw[i] = 0.35 * lum[i] + 0.65 * detail[i];
            }

        var smooth = Blur3(Blur3(raw, n), n);
        var height = new int[n * n];
        var color = new uint[n * n];
        var water = new bool[n * n];
        for (int i = 0; i < n * n; i++)
        {
            var (r, g, b, _) = PixelImage.Unpack(img.Rgba[i]);
            color[i] = (uint)(r << 16 | g << 8 | b);
            if (b > r + 20 && b > g - 5 && detail[i] < 0.15)
            {
                water[i] = true;
                height[i] = s.WaterLevel - 2;
            }
            else height[i] = s.MinGround + (int)Math.Round(smooth[i] * s.Relief, MidpointRounding.AwayFromZero);
        }
        return new FileMap(n, s.WaterLevel, WorldSeed.Of(img), height, color, water);
    }

    static int Clamp(int v, int n) => v < 0 ? 0 : v >= n ? n - 1 : v;

    static double[] Blur3(double[] src, int n)
    {
        var dst = new double[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                double sum = 0;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        sum += src[Clamp(z + dz, n) * n + Clamp(x + dx, n)];
                dst[z * n + x] = sum / 9;
            }
        return dst;
    }
}
