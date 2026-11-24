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

    // the middle entries paint most of the world (ground, leaves, walls), so they get their own spread and a floor;
    // each check has a little slack under its target so a guarded palette passes the guard again unchanged
    const double MinMiddleSpread = 0.28, TargetMiddleSpread = 0.30, MinMiddleTop = 0.33, TargetMiddleTop = 0.35, EndGap = 0.02;

    /// <summary>
    /// Keeps a gray, night or flat photo readable as a world (plan Review Focus 5). Hues and the order of the
    /// entries are kept; only brightness moves.
    /// 1. The darkest and lightest entries end at least 0.35 apart (stretched around their middle).
    /// 2. With 4 or more entries, the middle ones (all but the two ends) are spaced evenly over at least 0.30 of
    ///    brightness, the lightest of them at 0.35 or more, and the two ends stay outside that range. This happens
    ///    when the middle is too narrow, too dark, or has neighbours closer than half the even spacing.
    /// A palette that already meets both is returned unchanged, so applying the guard twice changes nothing.
    /// </summary>
    public static uint[] EnsureContrast(uint[] palette)
    {
        var result = (uint[])palette.Clone();
        if (result.Length < 2) return result;
        // entries from dark to light, ties in palette order: distinct ends even when every entry is equal
        int[] rank = Enumerable.Range(0, result.Length).OrderBy(i => Luma(result[i])).ThenBy(i => i).ToArray();
        int lo = rank[0], hi = rank[^1];
        double dark = Luma(result[lo]), light = Luma(result[hi]);
        if (light - dark < MinContrast)
        {
            double mid = (dark + light) / 2, newDark = mid - TargetContrast / 2, newLight = mid + TargetContrast / 2;
            if (newDark < 0) (newDark, newLight) = (0, TargetContrast);
            if (newLight > 1) (newDark, newLight) = (1 - TargetContrast, 1);
            result[lo] = WithLuma(result[lo], newDark);
            result[hi] = WithLuma(result[hi], newLight);
            (dark, light) = (newDark, newLight);
        }
        if (result.Length < 4) return result;

        var middle = rank[1..^1];
        double low = middle.Min(i => Luma(result[i])), high = middle.Max(i => Luma(result[i]));
        // neighbours must also be apart: one lamp color must not hide three equal darks (half the target spacing)
        double step = Enumerable.Range(1, middle.Length - 1).Min(k => Luma(result[middle[k]]) - Luma(result[middle[k - 1]]));
        if (high - low >= MinMiddleSpread && high >= MinMiddleTop && step >= TargetMiddleSpread / (middle.Length - 1) / 2)
            return result;
        double width = Math.Max(high - low, TargetMiddleSpread);
        double a = Math.Max(dark + EndGap, (low + high - width) / 2), b = a + width;
        if (b < TargetMiddleTop) (a, b) = (a + TargetMiddleTop - b, TargetMiddleTop);
        if (b > 1 - EndGap) (a, b) = (a - (b - (1 - EndGap)), 1 - EndGap);
        for (int k = 0; k < middle.Length; k++)
            result[middle[k]] = WithLuma(result[middle[k]], a + (b - a) * k / (middle.Length - 1));
        if (light < b + EndGap) result[hi] = WithLuma(result[hi], Math.Min(1, b + EndGap));
        if (dark > a - EndGap) result[lo] = WithLuma(result[lo], Math.Max(0, a - EndGap));
        return result;
    }

    /// <summary>The same hue at brightness `target` (0..1): darker by scaling, lighter by mixing toward white.</summary>
    public static uint WithLuma(uint rgb, double target)
    {
        double l = Luma(rgb);
        if (Math.Abs(l - target) < 1e-9) return rgb;
        return target < l ? Scale(rgb, target / l) : TowardWhite(rgb, l >= 1 ? 0 : (target - l) / (1 - l));
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
