namespace Hangbar.Core.World;

/// <summary>Where the sky meets the ground in a photo, so the first view tilts like the photo (v2 design: first view).</summary>
public static class Horizon
{
    /// <summary>
    /// 0 = top edge, 1 = bottom edge. The horizon is where the run of sky rows starting at the top ends.
    /// A photo without sky at the top, or all sky, gives 0.5.
    /// </summary>
    public static float Estimate(PixelImage prepared)
    {
        int h = prepared.Height, w = prepared.Width, skyRows = 0;
        for (int y = 0; y < h; y++)
        {
            int sky = 0;
            for (int x = 0; x < w; x++)
                if (IsSky(prepared[x, y])) sky++;
            if (sky * 10 < w * 7) break; // under 70% sky: the sky run is over
            skyRows++;
        }
        if (skyRows == 0 || skyRows == h) return 0.5f;
        return skyRows / (float)h;
    }

    /// <summary>Camera tilt for the first view: horizon high in the photo → looking down (negative). Clamped to −25…15°.</summary>
    public static float PitchDegrees(float horizon, float verticalFov = 50f) => Math.Clamp((horizon - 0.5f) * verticalFov, -25f, 15f);

    /// <summary>Blue-ish and fairly bright, or bright and colourless (overcast).</summary>
    static bool IsSky(uint px)
    {
        var (r, g, b, _) = PixelImage.Unpack(px);
        double luma = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        bool blue = b > r && b >= g - 10 && luma > 0.45;
        bool overcast = luma > 0.85 && max - min < 25;
        return blue || overcast;
    }
}
