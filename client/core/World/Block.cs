namespace Hangbar.Core.World;

/// <summary>
/// A block is one ushort: 0 = air, 1 = water, 2 + rgb15 = solid block of that color (5 bits per channel).
/// Material (stone, wood, metal) is derived from the color later (design §6-4).
/// </summary>
public static class Block
{
    public const ushort Air = 0, Water = 1;

    public static ushort Solid(uint rgb)
    {
        uint r = (rgb >> 19) & 0x1F, g = (rgb >> 11) & 0x1F, b = (rgb >> 3) & 0x1F;
        return (ushort)(2 + (r << 10 | g << 5 | b));
    }

    public static bool IsSolid(ushort b) => b >= 2;

    /// <summary>0xRRGGBB of a solid block (5-bit channels widened back to 8 bits).</summary>
    public static uint ColorOf(ushort b)
    {
        uint v = (uint)(b - 2);
        uint r = (v >> 10) & 0x1F, g = (v >> 5) & 0x1F, bl = v & 0x1F;
        return Widen(r) << 16 | Widen(g) << 8 | Widen(bl);
    }

    static uint Widen(uint c5) => c5 << 3 | c5 >> 2;
}
