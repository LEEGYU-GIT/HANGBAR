namespace Hangbar.Core.World;

/// <summary>Built-in pictures drawn in code: the world for a folder without photos, and test input.</summary>
public static class SamplePhotos
{
    /// <summary>A meadow with rolling hills, a rocky hilltop, a pond with a sandy shore and a few flowers. Deterministic.</summary>
    public static PixelImage Meadow(int size = 256)
    {
        var px = new uint[size * size];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                double u = (i + 0.5) / size, v = (j + 0.5) / size;
                double h = 0.45 + 0.2 * Math.Sin(u * 6 + 0.5) * Math.Cos(v * 5 - 0.3)
                         + 0.28 * (Noise(u * 5, v * 5) - 0.5) + 0.12 * (Noise(u * 13, v * 13) - 0.5);
                h += 0.35 * Math.Max(0, 1 - Math.Sqrt((u - 0.82) * (u - 0.82) + (v - 0.18) * (v - 0.18)) / 0.32);
                h = Math.Clamp(h, 0, 1);
                double d = Math.Sqrt((u - 0.3) * (u - 0.3) + (v - 0.64) * (v - 0.64));
                double r, g, b;
                if (d < 0.15) (r, g, b) = (0.25, 0.45, 0.82);
                else if (d < 0.2) (r, g, b) = (0.86, 0.78, 0.55);
                else if (h > 0.78) (r, g, b) = (0.6, 0.58, 0.56);
                else
                {
                    (r, g, b) = (0.27 + 0.33 * h, 0.5 + 0.25 * h, 0.22 + 0.1 * h);
                    double f = Hash(i * size / 48, j * size / 48 + 999);
                    if (f > 0.965) (r, g, b) = f > 0.983 ? (0.95, 0.55, 0.7) : (0.97, 0.85, 0.3);
                    // grass texture so meadows are not perfectly flat in the map
                    double t = 0.9 + 0.2 * Hash(i, j);
                    (r, g, b) = (r * t, g * t, b * t);
                }
                px[j * size + i] = PixelImage.Pack((int)(r * 255), (int)(g * 255), (int)(b * 255));
            }
        return new PixelImage(size, size, px);
    }

    static double Hash(int x, int y)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (int)((uint)n >> 13)) * 1274126177;
            return (uint)(n ^ (int)((uint)n >> 16)) / 4294967295.0;
        }
    }

    static double Noise(double x, double y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        double xf = x - xi, yf = y - yi, sx = xf * xf * (3 - 2 * xf), sy = yf * yf * (3 - 2 * yf);
        double a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
    }
}
