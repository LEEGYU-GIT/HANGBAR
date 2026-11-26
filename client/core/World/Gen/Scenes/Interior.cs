namespace Hangbar.Core.World.Gen;

/// <summary>A room inside an interior building (building coordinates p 0..L−1, q 0..D−1; 0 and L−1/D−1 are outer walls).</summary>
public sealed record InteriorRoom(string Kind, int P0, int P1, int Q0, int Q1, Material Floor);

/// <summary>A 4 × 9 doorway in the wall p = Wall (OnP) or q = Wall, spanning From..From+3 along it.</summary>
public sealed record InteriorDoor(bool OnP, int Wall, int From);

/// <summary>A one-storey building: size, outer wall material, rooms and doors. Outer walls of rooms get glass windows.</summary>
public sealed record InteriorPlan(int L, int D, Material Wall, IReadOnlyList<InteriorRoom> Rooms, IReadOnlyList<InteriorDoor> Doors,
    bool Windows = true, string Name = "building");

/// <summary>
/// A real-scale one-storey interior (v2 design §6-4; v2 part 2 plan Task 1), built the way the house scene builds its
/// floors: 1-block floor slab, 11 blocks of room, a flat roof with a parapet, 1-block walls, 4 × 9 doors, glass windows
/// 4–7 blocks above the floor. Rooms and doors are recorded as structures (room_*, doorway, and the building box).
/// Building coordinates (p, q) map to local (a0 + p, b0 + q) and through the frame to the world.
/// </summary>
public static class Interior
{
    public const int Ceiling = 11, DoorW = 4, DoorH = 9;

    public static InteriorBuilder Build(SceneContext ctx, LocalFrame f, int a0, int b0, InteriorPlan plan)
    {
        var w = ctx.World;
        int ground = SceneTools.MaxSurface(ctx, f, a0, b0, plan.L, plan.D), roof = ground + Ceiling, parapet = roof + 2;
        uint outer = Block.Solid(ctx.ColorOf(plan.Wall), plan.Wall);
        uint plaster = Block.Solid(ctx.Colors.Light, Material.Concrete);
        uint slab = Block.Solid(ctx.ColorOf(Material.Concrete), Material.Concrete);
        var b = new InteriorBuilder(ctx, f, a0, b0, ground, plan);

        for (int q = 0; q < plan.D; q++)
            for (int p = 0; p < plan.L; p++)
            {
                var (x, z) = b.W(p, q);
                bool edge = p == 0 || q == 0 || p == plan.L - 1 || q == plan.D - 1;
                SceneTools.Support(w, x, z, 1, 1, ground - 1, slab);
                for (int y = ground - 1; y <= parapet + 4; y++)
                {
                    uint blk = y == ground - 1 || y == roof ? slab
                        : y < roof ? (edge ? outer : plaster)
                        : y <= parapet && edge ? outer
                        : Block.Air;
                    w.Set(x, y, z, blk);
                }
            }

        foreach (var r in plan.Rooms)
        {
            uint floor = Block.Solid(ctx.ColorOf(r.Floor), r.Floor);
            for (int q = r.Q0; q < r.Q1; q++)
                for (int p = r.P0; p < r.P1; p++)
                {
                    b.Set(p, ground - 1, q, floor);
                    for (int y = ground; y < roof; y++) b.Set(p, y, q, Block.Air);
                }
            ctx.Structures.Add(b.Box("room_" + r.Kind, r.P0, r.Q0, r.P1 - r.P0, r.Q1 - r.Q0, ground, roof - 1));
        }

        foreach (var d in plan.Doors)
        {
            for (int k = 0; k < DoorW; k++)
            {
                var (p, q) = d.OnP ? (d.Wall, d.From + k) : (d.From + k, d.Wall);
                for (int y = ground; y < ground + DoorH; y++) b.Set(p, y, q, Block.Air);
            }
            ctx.Structures.Add(d.OnP ? b.Box("doorway", d.Wall, d.From, 1, DoorW, ground, ground + DoorH - 1)
                                     : b.Box("doorway", d.From, d.Wall, DoorW, 1, ground, ground + DoorH - 1));
        }

        if (plan.Windows) Windows(b, plan, Block.Solid(0xC8E6F0, Material.Glass));
        ctx.Structures.Add(b.Box(plan.Name, 0, 0, plan.L, plan.D, ground - 1, parapet));
        return b;
    }

    static void Windows(InteriorBuilder b, InteriorPlan plan, uint glass)
    {
        bool Door(bool onP, int wall, int c) => plan.Doors.Any(d => d.OnP == onP && d.Wall == wall && c >= d.From - 1 && c <= d.From + DoorW);
        foreach (var r in plan.Rooms)
        {
            var sides = new List<(bool OnP, int Wall, int From, int To)>();
            if (r.P0 == 1) sides.Add((true, 0, r.Q0, r.Q1));
            if (r.P1 == plan.L - 1) sides.Add((true, plan.L - 1, r.Q0, r.Q1));
            if (r.Q0 == 1) sides.Add((false, 0, r.P0, r.P1));
            if (r.Q1 == plan.D - 1) sides.Add((false, plan.D - 1, r.P0, r.P1));
            foreach (var (onP, wall, from, to) in sides)
            {
                int len = to - from;
                // windows every 12 blocks along the wall, 8 wide, skipping doors
                for (int c0 = from + 2; c0 + 8 <= to - 2 || (len < 12 && c0 == from + 2); c0 += 12)
                {
                    int width = Math.Min(8, len - 4);
                    if (width < 3) break;
                    for (int c = c0; c < c0 + width; c++)
                    {
                        if (Door(onP, wall, c)) continue;
                        for (int y = b.Floor + 4; y <= b.Floor + 7; y++)
                            if (onP) b.Set(wall, y, c, glass); else b.Set(c, y, wall, glass);
                    }
                    if (len < 12) break;
                }
            }
        }
    }
}

/// <summary>Places things inside an interior in building coordinates.</summary>
public sealed class InteriorBuilder(SceneContext ctx, LocalFrame f, int a0, int b0, int ground, InteriorPlan plan)
{
    /// <summary>Standing height of the floor (first air above the slab).</summary>
    public int Floor { get; } = ground;
    public InteriorPlan Plan { get; } = plan;
    public LocalFrame Frame { get; } = f;
    public int A0 { get; } = a0;
    public int B0 { get; } = b0;

    public (int X, int Z) W(int p, int q) => f.W(a0 + p, b0 + q);

    public void Set(int p, int y, int q, uint block)
    {
        var (x, z) = W(p, q);
        ctx.World.Set(x, y, z, block);
    }

    public PlacedPart Box(string name, int p0, int q0, int wp, int wq, int y0, int y1) => f.Box(name, a0 + p0, b0 + q0, wp, wq, y0, y1);

    public Facing Toward(int dp, int dq) => f.FacingOf(dp, dq);

    /// <summary>A part with its front toward (dp, dq), its footprint's min corner at (p0, q0), on the floor.</summary>
    public PlacedPart Furn(string name, int p0, int q0, int dp, int dq, int? stretch = null, int up = 0)
    {
        var part = PartLibrary.Get(name);
        int width = stretch ?? part.Width;
        bool alongP = dp != 0;
        int wp = alongP ? part.Depth : width, wq = alongP ? width : part.Depth;
        var placed = PartPlacer.Place(ctx.World, part, f.Origin(a0 + p0, b0 + q0, wp, wq, Floor + up), LocalFrame.FrontTo(f.FacingOf(dp, dq)),
            ctx.Colors, new PlaceOptions(StretchTo: stretch), ctx.Rng);
        ctx.Structures.Add(placed);
        return placed;
    }

    /// <summary>A small round thing standing <paramref name="up"/> blocks above the floor (on a table top: 3).</summary>
    public void Thing(PropKind kind, int p, int q, int up, float size = 0.5f, uint? color = null)
    {
        var (x, z) = W(p, q);
        ctx.Props.Add(new Prop(kind, x, z, size, color ?? ctx.Colors.Accent, Floor + up));
    }

    /// <summary>A framed photo on the inner face of the wall q = wall (or p = wall), facing (dp, dq), from c0 to c1.</summary>
    public MuralSpot Mural(bool onP, int wall, int c0, int c1, int dp, int dq, int height = 8, int up = 2) =>
        LocalFrame.Spot(onP ? W(wall, c0) : W(c0, wall), onP ? W(wall, c1) : W(c1, wall), Floor + up, f.FacingOf(dp, dq), height, framed: true);

    public void MarkStairs(int p0, int q0, int wp, int wq)
    {
        for (int q = q0; q < q0 + wq; q++)
            for (int p = p0; p < p0 + wp; p++) ctx.Stairs[f.I(a0 + p, b0 + q)] = true;
    }
}
