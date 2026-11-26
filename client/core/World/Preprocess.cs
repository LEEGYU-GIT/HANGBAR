namespace Hangbar.Core.World;

/// <summary>Model input (G0.5 Task 4): a photo as a channel-first float tensor.</summary>
public static class Preprocess
{
    /// <summary>
    /// Centre square → size×size (<see cref="ImageFit.Prepare"/>, which also flattens transparency) → three planes
    /// R, G, B of size² values each, every value (v/255 − mean)/std.
    /// </summary>
    public static float[] ToChw(PixelImage img, int size, float[] mean, float[] std)
    {
        var fit = ImageFit.Prepare(img, size);
        int plane = size * size;
        var t = new float[3 * plane];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var (r, g, b, _) = PixelImage.Unpack(fit[x, y]);
                int i = y * size + x;
                t[i] = (r / 255f - mean[0]) / std[0];
                t[plane + i] = (g / 255f - mean[1]) / std[1];
                t[2 * plane + i] = (b / 255f - mean[2]) / std[2];
            }
        return t;
    }
}
