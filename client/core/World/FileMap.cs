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
