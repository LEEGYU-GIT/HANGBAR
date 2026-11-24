namespace Hangbar.Core.World;

/// <summary>A photo's representative colors (design §6-3: ground, dirt, water and tree colors come from these).</summary>
public static class Palette
{
    const double MinContrast = 0.35, TargetContrast = 0.36; // aim a little above so 8-bit rounding never lands under

    /// <summary>
    /// Median cut: `count` colors (0xRRGGBB) ordered dark to light. The box with the most pixels × widest channel
    /// range is split at its median until there are `count` boxes; a photo with fewer distinct colors repeats
    /// colors to fill the count. Deterministic.
    /// </summary>
    public static uint[] Extract(PixelImage prepared, int count = 6)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        var boxes = new List<uint[]> { prepared.Rgba.Select(p => p >> 8).ToArray() };
        while (boxes.Count < count)
        {
            int best = -1;
            long bestScore = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                long score = (long)boxes[i].Length * Widest(boxes[i]).Range;
                if (boxes[i].Length >= 2 && score > bestScore) (best, bestScore) = (i, score);
            }
            if (best < 0) break; // every box is a single color
            var box = boxes[best];
            int shift = Widest(box).Shift;
            var sorted = box.OrderBy(c => (c >> shift) & 0xFF).ThenBy(c => c).ToArray();
            int half = sorted.Length / 2;
            boxes[best] = sorted[..half];
            boxes.Insert(best + 1, sorted[half..]);
        }
        var colors = boxes.Select(Average).ToList();
        for (int i = 0; colors.Count < count; i++) colors.Add(colors[i]);
        return colors.OrderBy(Luma).ThenBy(c => c).ToArray();
    }

    /// <summary>
    /// When the darkest and lightest colors are less than 0.35 apart in brightness (a gray or night photo), darkens
    /// the darkest and lightens the lightest around their middle so the world stays readable (plan Review Focus 5).
    /// Hues are kept; the order of the palette is kept.
    /// </summary>
    public static uint[] EnsureContrast(uint[] palette)
    {
        var result = (uint[])palette.Clone();
        if (result.Length < 2) return result;
        int lo = 0, hi = 0; // first darkest, last lightest: different entries even when all are equal
        for (int i = 1; i < result.Length; i++)
        {
            if (Luma(result[i]) < Luma(result[lo])) lo = i;
            if (Luma(result[i]) >= Luma(result[hi])) hi = i;
        }
        double dark = Luma(result[lo]), light = Luma(result[hi]);
        if (light - dark >= MinContrast) return result;

        double mid = (dark + light) / 2, newDark = mid - TargetContrast / 2, newLight = mid + TargetContrast / 2;
        if (newDark < 0) (newDark, newLight) = (0, TargetContrast);
        if (newLight > 1) (newDark, newLight) = (1 - TargetContrast, 1);
        result[lo] = Scale(result[lo], dark <= 0 ? 0 : newDark / dark);
        result[hi] = TowardWhite(result[hi], light >= 1 ? 0 : (newLight - light) / (1 - light));
        return result;
    }

    /// <summary>Perceived brightness 0..1 of 0xRRGGBB.</summary>
    public static double Luma(uint rgb) =>
        (0.299 * ((rgb >> 16) & 0xFF) + 0.587 * ((rgb >> 8) & 0xFF) + 0.114 * (rgb & 0xFF)) / 255.0;

    static (int Shift, int Range) Widest(uint[] box)
    {
        (int Shift, int Range) best = (16, -1);
        foreach (int shift in new[] { 16, 8, 0 })
        {
            uint min = 255, max = 0;
            foreach (uint c in box)
            {
                uint v = (c >> shift) & 0xFF;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if ((int)(max - min) > best.Range) best = (shift, (int)(max - min));
        }
        return best;
    }

    static uint Average(uint[] box)
    {
        long r = 0, g = 0, b = 0;
        foreach (uint c in box) { r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF; }
        uint Ch(long sum) => (uint)Math.Round((double)sum / box.Length);
        return Ch(r) << 16 | Ch(g) << 8 | Ch(b);
    }

    static uint Scale(uint rgb, double k)
    {
        uint Ch(int s) => (uint)Math.Clamp((int)Math.Round(((rgb >> s) & 0xFF) * k), 0, 255);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    static uint TowardWhite(uint rgb, double t)
    {
        uint Ch(int s) { double v = (rgb >> s) & 0xFF; return (uint)Math.Clamp((int)Math.Round(v + (255 - v) * t), 0, 255); }
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }
}

/// <summary>A photo's light and mood, each 0..1. Warmth 0.5 is neutral, above is red-leaning, below is blue-leaning.</summary>
public sealed record Mood(float Brightness, float Saturation, float Warmth);

public static class MoodReader
{
    public static Mood Read(PixelImage prepared)
    {
        double luma = 0, sat = 0, red = 0, blue = 0;
        foreach (uint p in prepared.Rgba)
        {
            var (r, g, b, _) = PixelImage.Unpack(p);
            int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            luma += 0.299 * r + 0.587 * g + 0.114 * b;
            sat += max == 0 ? 0 : (max - min) / (double)max;
            red += r;
            blue += b;
        }
        int n = prepared.Rgba.Length;
        return new Mood(
            (float)(luma / n / 255),
            (float)(sat / n),
            (float)Math.Clamp(0.5 + (red - blue) / n / 510, 0, 1));
    }
}
