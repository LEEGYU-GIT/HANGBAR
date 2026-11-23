namespace Hangbar.Core.World;

/// <summary>Turns any photo into the square map image a file map is built from (design §6-3).</summary>
public static class ImageFit
{
    /// <summary>What fully or mostly transparent pixels become, so a transparent PNG has no black holes.</summary>
    public const uint TransparentFill = 0xD8D8D8FF;

    /// <summary>Center square crop → transparency flattened → size×size (area average down, nearest up).</summary>
    public static PixelImage Prepare(PixelImage src, int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        int side = Math.Min(src.Width, src.Height);
        int x0 = (src.Width - side) / 2, y0 = (src.Height - side) / 2;
        return side >= size ? Downscale(src, x0, y0, side, size) : Upscale(src, x0, y0, side, size);
    }

    static uint Flatten(uint p) => (p & 0xFF) < 128 ? TransparentFill : p | 0xFF;

    static PixelImage Upscale(PixelImage src, int x0, int y0, int side, int size)
    {
        var dst = new uint[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                dst[y * size + x] = Flatten(src[x0 + x * side / size, y0 + y * side / size]);
        return new PixelImage(size, size, dst);
    }

    /// <summary>Exact area average: each output pixel weighs every source pixel by its overlap.</summary>
    static PixelImage Downscale(PixelImage src, int x0, int y0, int side, int size)
    {
        var dst = new uint[size * size];
        double scale = (double)side / size;
        for (int oy = 0; oy < size; oy++)
        {
            double sy0 = oy * scale, sy1 = sy0 + scale;
            for (int ox = 0; ox < size; ox++)
            {
                double sx0 = ox * scale, sx1 = sx0 + scale;
                double r = 0, g = 0, b = 0, wsum = 0;
                for (int sy = (int)sy0; sy < Math.Min(side, (int)Math.Ceiling(sy1)); sy++)
                {
                    double wy = Math.Min(sy + 1, sy1) - Math.Max(sy, sy0);
                    for (int sx = (int)sx0; sx < Math.Min(side, (int)Math.Ceiling(sx1)); sx++)
                    {
                        double w = wy * (Math.Min(sx + 1, sx1) - Math.Max(sx, sx0));
                        var (pr, pg, pb, _) = PixelImage.Unpack(Flatten(src[x0 + sx, y0 + sy]));
                        r += pr * w; g += pg * w; b += pb * w; wsum += w;
                    }
                }
                dst[oy * size + ox] = PixelImage.Pack((int)Math.Round(r / wsum), (int)Math.Round(g / wsum), (int)Math.Round(b / wsum));
            }
        }
        return new PixelImage(size, size, dst);
    }
}
