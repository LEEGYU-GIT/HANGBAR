namespace Hangbar.Core.World;

/// <summary>What a block is made of (v2 design). Pixel = photo mural and v1 blocks; DarkGlass = opaque windows of facade-only buildings.</summary>
public enum Material : byte
{
    Air = 0, Water = 1, Pixel = 2, Soil = 3, Grass = 4, Sand = 5, Stone = 6, DeepStone = 7, Bedrock = 8,
    Snow = 9, Wood = 10, Brick = 11, Concrete = 12, Tile = 13, Metal = 14, Glass = 15, DarkGlass = 16, Cloth = 17,
}

/// <summary>Wear on a block. 4–7 are reserved.</summary>
public enum BlockState : byte { Intact = 0, Cracked = 1, Mossy = 2, Worn = 3 }

/// <summary>
/// A block is one uint: color in bits 0–23 (0xRRGGBB), material in bits 24–28, state in bits 29–31.
/// 0 is air.
/// </summary>
public static class Block
{
    public const uint Air = 0;
    public static readonly uint Water = Make(Material.Water, 0);

    const int MaterialShift = 24, StateShift = 29;
    const uint ColorMask = 0xFFFFFF, MaterialMask = 0x1F, StateMask = 0x7;

    public static uint Make(Material m, uint rgb, BlockState s = BlockState.Intact) =>
        (rgb & ColorMask) | ((uint)m & MaterialMask) << MaterialShift | ((uint)s & StateMask) << StateShift;

    /// <summary>A solid block. Air and water are not solid materials.</summary>
    public static uint Solid(uint rgb, Material m = Material.Pixel, BlockState s = BlockState.Intact)
    {
        if (m is Material.Air or Material.Water) throw new ArgumentException($"{m} is not a solid material.", nameof(m));
        return Make(m, rgb, s);
    }

    public static Material MaterialOf(uint b) => (Material)((b >> MaterialShift) & MaterialMask);
    public static BlockState StateOf(uint b) => (BlockState)((b >> StateShift) & StateMask);
    public static uint ColorOf(uint b) => b & ColorMask;
    public static uint WithState(uint b, BlockState s) => (b & ~(StateMask << StateShift)) | ((uint)s & StateMask) << StateShift;

    public static bool IsSolid(uint b) => MaterialOf(b) >= Material.Pixel;
    public static bool IsWater(uint b) => MaterialOf(b) == Material.Water;
    /// <summary>Solid and not see-through glass.</summary>
    public static bool IsOpaque(uint b) => IsSolid(b) && MaterialOf(b) != Material.Glass;
    public static bool IsBreakable(uint b) => IsSolid(b) && MaterialOf(b) != Material.Bedrock;
}
