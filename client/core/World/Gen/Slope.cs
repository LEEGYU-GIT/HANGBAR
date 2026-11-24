namespace Hangbar.Core.World.Gen;

/// <summary>Height-field slope limiting (the v1 "Shape", generalised to any step).</summary>
public static class Slope
{
    public const int NoPin = int.MinValue;
    const int Far = 1 << 20;

    /// <summary>
    /// Limits every side step to <paramref name="step"/> blocks and holds pinned columns at their heights:
    /// the lower envelope of the heights, clamped between the lowest and highest cones the pins allow.
    /// Throws when two pins are too steep for each other.
    /// </summary>
    public static int[] Limit(int[] heights, int[] pins, int size, int step)
    {
        var env = (int[])heights.Clone();
        MinPlus(env, size, step);
        var up = new int[pins.Length];
        var lo = new int[pins.Length];
        for (int i = 0; i < pins.Length; i++)
        {
            up[i] = pins[i] == NoPin ? Far : pins[i];
            lo[i] = pins[i] == NoPin ? Far : -pins[i];
        }
        MinPlus(up, size, step);
        MinPlus(lo, size, step);
        var result = new int[heights.Length];
        for (int i = 0; i < result.Length; i++)
        {
            int low = -lo[i];
            if (low > up[i]) throw new InvalidOperationException("pinned heights are too steep for each other");
            result[i] = Math.Min(Math.Max(env[i], low), up[i]);
        }
        return result;
    }

    /// <summary>a[p] = min over q of a[q] + step·(grid distance p..q), in two sweeps.</summary>
    static void MinPlus(int[] a, int n, int step)
    {
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                if (x > 0) a[i] = Math.Min(a[i], a[i - 1] + step);
                if (z > 0) a[i] = Math.Min(a[i], a[i - n] + step);
            }
        for (int z = n - 1; z >= 0; z--)
            for (int x = n - 1; x >= 0; x--)
            {
                int i = z * n + x;
                if (x < n - 1) a[i] = Math.Min(a[i], a[i + 1] + step);
                if (z < n - 1) a[i] = Math.Min(a[i], a[i + n] + step);
            }
    }
}
