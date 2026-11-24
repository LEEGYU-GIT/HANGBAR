namespace Hangbar.Core.World.Gen;

public enum ColorRole { Primary, Secondary, Accent, Dark, Light }

/// <summary>
/// Five named colours taken from a photo palette (sorted dark → light), so building parts can say "primary"
/// instead of a fixed colour. Positions blend neighbouring palette colours, as the v1 generator did.
/// </summary>
public sealed record PaletteRoles(uint Primary, uint Secondary, uint Accent, uint Dark, uint Light)
{
    public uint this[ColorRole r] => r switch
    {
        ColorRole.Primary => Primary,
        ColorRole.Secondary => Secondary,
        ColorRole.Accent => Accent,
        ColorRole.Dark => Dark,
        _ => Light,
    };

    public static PaletteRoles From(uint[] palette)
    {
        if (palette.Length == 0) throw new ArgumentException("palette is empty", nameof(palette));
        return new PaletteRoles(At(palette, 0.55), At(palette, 0.35), Liveliest(palette), At(palette, 0.1), At(palette, 0.9));
    }

    /// <summary>Palette colour at t (0 = darkest, 1 = lightest), blended between neighbours.</summary>
    public static uint At(uint[] palette, double t)
    {
        double pos = Math.Clamp(t, 0, 1) * (palette.Length - 1);
        int i = (int)Math.Floor(pos);
        return Lerp(palette[i], palette[Math.Min(i + 1, palette.Length - 1)], pos - i);
    }

    /// <summary>The v1 leaf rule: the most saturated of the middle colours (both ends skipped when there are enough).</summary>
    static uint Liveliest(uint[] palette)
    {
        var middle = palette.Length > 2 ? palette.Skip(1).Take(palette.Length - 2) : palette;
        return middle.OrderByDescending(Saturation).ThenBy(c => c).First();
    }

    public static double Saturation(uint c)
    {
        int r = (int)(c >> 16 & 0xFF), g = (int)(c >> 8 & 0xFF), b = (int)(c & 0xFF), max = Math.Max(r, Math.Max(g, b));
        return max == 0 ? 0 : (max - Math.Min(r, Math.Min(g, b))) / (double)max;
    }

    public static uint Lerp(uint a, uint b, double t)
    {
        uint Ch(int s) => (uint)Math.Round((a >> s & 0xFF) + ((double)(b >> s & 0xFF) - (a >> s & 0xFF)) * t);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }
}
