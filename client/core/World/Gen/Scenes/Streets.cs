namespace Hangbar.Core.World.Gen;

/// <summary>Where a road lies across b (local frame), all ranges [from, to).</summary>
public sealed record RoadInfo(int CarriageFrom, int CarriageTo, int SidewalkFrom, int SidewalkTo);

/// <summary>
/// Streets for the town scenes (v2 part 2 plan Task 1): roads with lanes and raised sidewalks, crosswalks, and
/// outside-only buildings (§6-11: solid boxes with dark windows; one or two buildings per world are enterable,
/// built with <see cref="Interior"/> instead).
/// </summary>
public static class Streets
{
    public const int LaneWidth = 12; // 3 m

    /// <summary>
    /// A road along a (a0..a1−1) centred on bCenter: concrete carriageway of <paramref name="lanes"/> lanes with a solid
    /// tile centre line and dashed lane lines, and brick sidewalks one block higher on both sides. Sidewalks are paths
    /// (walked onto without a jump); the whole road keeps caves away. Heights come from the surface at the centre.
    /// </summary>
    public static RoadInfo Road(SceneContext ctx, LocalFrame f, int a0, int a1, int bCenter, int lanes, int sidewalk)
    {
        int half = lanes * LaneWidth / 2, c0 = bCenter - half, c1 = bCenter + half;
        for (int a = a0; a < a1; a++)
        {
            int ground = ctx.Surface[f.I(a, bCenter)];
            for (int b = c0 - sidewalk; b < c1 + sidewalk; b++)
            {
                int i = f.I(a, b);
                ctx.NoCaves[i] = true;
                ctx.WaterTop[i] = 0;
                if (b < c0 || b >= c1)
                {
                    ctx.Surface[i] = ground + 1;
                    ctx.TopMaterial[i] = Material.Brick;
                    ctx.Path[i] = true;
                    continue;
                }
                ctx.Surface[i] = ground;
                int k = b - c0;
                bool centre = lanes > 1 && (k == half || k == half - 1);
                bool laneLine = k > 0 && k % LaneWidth == 0 && !centre && (a / 8) % 2 == 0;
                ctx.TopMaterial[i] = centre || laneLine ? Material.Tile : Material.Concrete;
            }
        }
        return new RoadInfo(c0, c1, c0 - sidewalk, c1 + sidewalk);
    }

    /// <summary>Zebra stripes across the carriageway, 12 wide along a, centred on aCenter.</summary>
    public static void Crosswalk(SceneContext ctx, LocalFrame f, int aCenter, RoadInfo road)
    {
        for (int b = road.CarriageFrom; b < road.CarriageTo; b++)
            for (int a = aCenter - 6; a < aCenter + 6; a++)
                ctx.TopMaterial[f.I(a, b)] = (b - road.CarriageFrom) / 3 % 2 == 0 ? Material.Tile : Material.Concrete;
    }

    /// <summary>
    /// An outside-only building: a solid box of <paramref name="floors"/> storeys (12 blocks each) on the highest
    /// ground under it, dark glass windows on every outer face (rows 4–7 of each storey, 4 of every 6 columns), a
    /// closed wooden door and a shop window on the ground floor of its front, a flat concrete roof. Recorded as a
    /// structure named <paramref name="name"/>.
    /// </summary>
    public static PlacedPart Building(SceneContext ctx, LocalFrame f, int a0, int b0, int wa, int wb, int floors, (int Da, int Db) front,
        Material wall, string name = "facade")
    {
        var w = ctx.World;
        int ground = SceneTools.MaxSurface(ctx, f, a0, b0, wa, wb), top = ground + floors * 12;
        double tint = 0.85 + 0.3 * ctx.Rng.Next();
        uint wallBlock = Block.Solid(Shade(ctx.ColorOf(wall), tint), wall);
        uint glass = Block.Solid(0x2E3A44, Material.DarkGlass), door = Block.Solid(0x4A3020, Material.Wood);
        uint roof = Block.Solid(ctx.ColorOf(Material.Concrete), Material.Concrete);
        for (int b = b0; b < b0 + wb; b++)
            for (int a = a0; a < a0 + wa; a++)
            {
                var (x, z) = f.W(a, b);
                SceneTools.Support(w, x, z, 1, 1, ground - 1, wallBlock);
                bool edge = a == a0 || b == b0 || a == a0 + wa - 1 || b == b0 + wb - 1;
                // which face this column shows, and its position along that face
                int along = a == a0 || a == a0 + wa - 1 ? b - b0 : a - a0;
                bool onFront = (front.Db < 0 && b == b0) || (front.Db > 0 && b == b0 + wb - 1)
                               || (front.Da < 0 && a == a0) || (front.Da > 0 && a == a0 + wa - 1);
                int len = front.Da != 0 ? wb : wa, mid = len / 2;
                for (int y = ground - 1; y <= top; y++)
                {
                    uint blk = y == top ? roof : wallBlock;
                    if (edge && y < top)
                    {
                        int storey = (y - ground) / 12, row = (y - ground) % 12;
                        if (onFront && storey == 0)
                        {
                            if (Math.Abs(along - mid) <= 2 && row < 9) blk = door;
                            else if (row is >= 3 and <= 8 && along % 8 is >= 1 and <= 5) blk = glass;
                        }
                        else if (row is >= 4 and <= 7 && along % 6 is >= 1 and <= 4 && along > 0 && along < len - 1) blk = glass;
                    }
                    w.Set(x, y, z, blk);
                }
            }
        var placed = f.Box(name, a0, b0, wa, wb, ground - 1, top);
        ctx.Structures.Add(placed);
        return placed;
    }

    static uint Shade(uint c, double k)
    {
        uint Ch(int s) => (uint)Math.Clamp(Math.Round((c >> s & 0xFF) * k), 0, 255);
        return Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }
}
