namespace Hangbar.Core.World.Gen;

/// <summary>
/// Fills the world from the column arrays: bedrock (y 0–1), deep stone up to y 19–23, stone, 3–5 blocks of soil,
/// the top material, then water up to WaterTop (v2 design: underground layers).
/// </summary>
public static class Strata
{
    public const uint BedrockColor = 0x2A2A2E;

    public static void Fill(SceneContext ctx)
    {
        int n = SceneContext.Size;
        var w = ctx.World = new VoxelWorld(n, SceneContext.Height, n);
        uint bedrock = Block.Solid(BedrockColor, Material.Bedrock);
        uint deep = Block.Solid(ctx.ColorOf(Material.DeepStone), Material.DeepStone);
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        uint soil = Block.Solid(ctx.ColorOf(Material.Soil), Material.Soil);
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = ctx.Index(x, z), top = Math.Clamp(ctx.Surface[i], 3, SceneContext.Height - 8) - 1;
                int deepTop = 19 + (int)Math.Floor(5 * ctx.Noise.Sample(x, z, 24, 501));
                int soilDepth = 3 + (int)Math.Floor(3 * ctx.Noise.Sample(x, z, 12, 502));
                var topMat = ctx.TopMaterial[i];
                uint topBlock = Block.Solid(Shade(ctx.ColorOf(topMat), 0.95 + 0.1 * ctx.Noise.Sample(x, z, 3, 503)), topMat);
                for (int y = 0; y <= top; y++)
                {
                    uint b = y <= 1 ? bedrock
                        : y == top ? topBlock
                        : y >= top - soilDepth ? soil
                        : y <= deepTop ? deep
                        : stone;
                    w.Set(x, y, z, b);
                }
                for (int y = top + 1; y < Math.Min(ctx.WaterTop[i], SceneContext.Height); y++) w.Set(x, y, z, Block.Water);
            }
    }

    /// <summary>
    /// Base colours of the ground materials from the palette roles. Soil, stone and grass are kept at least 0.06 apart
    /// in brightness around the palette's own brightness, so an all-white snow photo or an all-black night photo
    /// still shows where grass ends and stone begins.
    /// </summary>
    public static Dictionary<Material, uint> MaterialColors(PaletteRoles c)
    {
        double l = Palette.Luma(c.Primary);
        double stoneL = Math.Clamp(l, 0.2, 0.83);
        var m = new Dictionary<Material, uint>
        {
            [Material.Soil] = SetLuma(PaletteRoles.Lerp(c.Dark, 0x6B4A2B, 0.5), stoneL - 0.12),
            [Material.Stone] = SetLuma(Gray(c.Secondary, 0.7), stoneL),
            [Material.Grass] = SetLuma(c.Accent, stoneL + 0.12),
            [Material.DeepStone] = SetLuma(Gray(c.Secondary, 0.8), Math.Max(0.06, stoneL - 0.2)),
            [Material.Sand] = SetLuma(PaletteRoles.Lerp(c.Light, 0xE8D2A0, 0.5), Math.Min(0.92, stoneL + 0.2)),
            [Material.Snow] = SetLuma(PaletteRoles.Lerp(c.Light, 0xF0F4F8, 0.8), 0.93),
            [Material.Bedrock] = BedrockColor,
            [Material.Concrete] = Gray(c.Primary, 0.8),
            [Material.Tile] = c.Light,
            [Material.Wood] = PaletteRoles.Lerp(c.Dark, 0x8A5A30, 0.5),
            [Material.Brick] = PaletteRoles.Lerp(c.Primary, 0xA0502E, 0.5),
        };
        return m;
    }

    static uint Gray(uint c, double amount)
    {
        uint y = (uint)Math.Round(Palette.Luma(c) * 255);
        return PaletteRoles.Lerp(c, y << 16 | y << 8 | y, amount);
    }

    static uint Shade(uint c, double k)
    {
        uint Ch(int s) => (uint)Math.Clamp(Math.Round((c >> s & 0xFF) * k), 0, 255);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    /// <summary>The colour moved toward white or black until its brightness is <paramref name="target"/> (hue kept as far as it goes).</summary>
    public static uint SetLuma(uint c, double target)
    {
        target = Math.Clamp(target, 0.02, 0.98);
        double now = Palette.Luma(c);
        uint toward = now < target ? 0xFFFFFFu : 0x000000u;
        double lo = 0, hi = 1;
        for (int i = 0; i < 24; i++)
        {
            double mid = (lo + hi) / 2;
            double l = Palette.Luma(PaletteRoles.Lerp(c, toward, mid));
            if (now < target ? l < target : l > target) lo = mid; else hi = mid;
        }
        return PaletteRoles.Lerp(c, toward, hi);
    }
}

/// <summary>The outer 16 columns settle smoothly to the base ground and hold no water at the very edge.</summary>
public static class EdgeBand
{
    public static void Apply(SceneContext ctx)
    {
        int n = SceneContext.Size, band = SceneContext.EdgeBand;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int d = Math.Min(Math.Min(x, z), Math.Min(n - 1 - x, n - 1 - z));
                if (d >= band) continue;
                int i = ctx.Index(x, z);
                double t = d / (double)band, s = t * t * (3 - 2 * t);
                ctx.Surface[i] = (int)Math.Round(SceneContext.BaseGround + (ctx.Surface[i] - SceneContext.BaseGround) * s);
                if (d == 0 || ctx.Surface[i] >= ctx.WaterTop[i]) ctx.WaterTop[i] = 0;
            }
    }
}

/// <summary>The world's top view as a file map: per column the height above the top solid block, its colour, water.</summary>
public static class SurfaceMap
{
    public static FileMap Of(VoxelWorld w, ulong seed)
    {
        int n = w.SizeX;
        var height = new int[n * n];
        var color = new uint[n * n];
        var water = new bool[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                for (int y = w.SizeY - 1; y >= 0; y--)
                {
                    uint b = w.Get(x, y, z);
                    if (b == Block.Air) continue;
                    if (Block.IsWater(b)) { water[i] = true; continue; }
                    height[i] = y + 1;
                    color[i] = Block.ColorOf(b);
                    break;
                }
            }
        return new FileMap(n, SceneContext.WaterLevel, seed, height, color, water);
    }
}
