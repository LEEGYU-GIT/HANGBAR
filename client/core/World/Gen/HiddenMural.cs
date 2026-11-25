namespace Hangbar.Core.World.Gen;

/// <summary>
/// One photo mural per map, hidden from the spawn (v2 design: the hidden mural). A spot is good when
/// ① from the spawn's eye the picture's centre and four corners are all out of sight,
/// ② it is at least 40 columns from the spawn, and
/// ③ someone can walk to a cell 1–2 blocks in front of it.
/// Candidates are tried in order; when none works a free-standing 24×18 wall goes up far away, facing away from the spawn.
/// </summary>
public static class HiddenMural
{
    public const int MinDistance = 40, FallbackWidth = 24, FallbackHeight = 18, FallbackApron = 6;
    const double EyeHeight = 6.4;

    public static MuralSpot Place(SceneContext ctx, SceneView view)
    {
        var w = ctx.World;
        var reach = Walkability.ReachCells(w, view.Spawn);
        foreach (var spot in view.MuralCandidates)
            if (TryStamp(ctx, view.Spawn, spot, reach)) return spot;

        // fallback: from the farthest reachable cells inward, a wall between the cell and the spawn, picture facing the cell
        // stand cells away from the map edge: someone 0.6 m wide must fit between the picture and the edge wall
        int n = w.SizeX;
        var far = reach.Where(p => Math.Min(Math.Min(p.X, p.Z), Math.Min(n - 1 - p.X, n - 1 - p.Z)) >= FallbackApron)
            .Where(p => Math.Max(Math.Abs(p.X - view.Spawn.X), Math.Abs(p.Z - view.Spawn.Z)) >= MinDistance + 4)
            .OrderByDescending(p => Dist(p, view.Spawn)).ThenBy(p => p.X).ThenBy(p => p.Z).ThenBy(p => p.Y);
        MuralSpot? last = null;
        int tries = 0;
        foreach (var p in far)
        {
            if (++tries > 400) break;
            var spot = WallFor(ctx, view.Spawn, p);
            if (spot is null) continue;
            last = spot;
            var backup = Backup(w, spot);
            BuildWall(ctx, spot);
            if (IsHidden(w, view.Spawn, spot) && Distance(view.Spawn, spot) >= MinDistance &&
                IsReachable(Walkability.ReachCells(w, view.Spawn), spot))
                return spot;
            Restore(w, backup);
        }
        // nothing passed every rule: still put the picture somewhere far (never fails)
        last ??= new MuralSpot(new BlockPos(2, view.Spawn.Y, 2), Facing.South, FallbackWidth, FallbackHeight, false);
        BuildWall(ctx, last);
        return last;
    }

    /// <summary>Unit step to the viewer's right as they stand in front of a mural facing this way.</summary>
    public static (int X, int Z) RightOf(Facing f) => f switch
    {
        Facing.North => (-1, 0), // viewer looks south (+z): right is west
        Facing.South => (1, 0),  // viewer looks north: right is east
        Facing.East => (0, -1),  // viewer looks west: right is north
        _ => (0, 1),             // West: viewer looks east: right is south
    };

    /// <summary>Unit step out of the picture, toward its viewer.</summary>
    public static (int X, int Z) NormalOf(Facing f) => f switch
    {
        Facing.North => (0, -1),
        Facing.South => (0, 1),
        Facing.East => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>
    /// Rule ①: the centre and the four corners are all hidden from the spawn's eye, and so is the picture's back:
    /// photo blocks show the photo on every face, so a wall one block thick would show it mirrored from behind.
    /// Every picture block whose cell behind is not opaque has its back face checked.
    /// </summary>
    public static bool IsHidden(VoxelWorld w, BlockPos spawn, MuralSpot m)
    {
        var eye = (spawn.X + 0.5, spawn.Y + EyeHeight, spawn.Z + 0.5);
        foreach (var p in FacePoints(m))
            if (Sight.Clear(w, eye, p)) return false;
        var (rx, rz) = RightOf(m.Facing);
        var (nx, nz) = NormalOf(m.Facing);
        for (int c = 0; c < m.Width; c++)
            for (int r = 0; r < m.Height; r++)
            {
                int bx = m.Origin.X + rx * c - nx, by = m.Origin.Y + r, bz = m.Origin.Z + rz * c - nz;
                if (Block.IsOpaque(w.Get(bx, by, bz))) continue;
                var back = (m.Origin.X + 0.5 + rx * c - nx * 0.51, by + 0.5, m.Origin.Z + 0.5 + rz * c - nz * 0.51);
                if (Sight.Clear(w, eye, back)) return false;
            }
        return true;
    }

    /// <summary>Rule ②: horizontal distance from the spawn to the picture's centre.</summary>
    public static double Distance(BlockPos spawn, MuralSpot m)
    {
        var (rx, rz) = RightOf(m.Facing);
        double cx = m.Origin.X + rx * (m.Width - 1) / 2.0, cz = m.Origin.Z + rz * (m.Width - 1) / 2.0;
        return Math.Sqrt((cx - spawn.X) * (cx - spawn.X) + (cz - spawn.Z) * (cz - spawn.Z));
    }

    /// <summary>Rule ③: some reachable cell stands 1–2 blocks in front of the picture's bottom row.</summary>
    public static bool IsReachable(HashSet<BlockPos> reach, MuralSpot m)
    {
        var (rx, rz) = RightOf(m.Facing);
        var (nx, nz) = NormalOf(m.Facing);
        var columns = new HashSet<(int, int)>();
        for (int c = 0; c < m.Width; c++)
            for (int k = 1; k <= 2; k++)
                columns.Add((m.Origin.X + rx * c + nx * k, m.Origin.Z + rz * c + nz * k));
        return reach.Any(p => columns.Contains((p.X, p.Z)) && Math.Abs(p.Y - m.Origin.Y) <= 3);
    }

    /// <summary>Centre and corners of the picture, just in front of its face (block coordinates).</summary>
    static IEnumerable<(double X, double Y, double Z)> FacePoints(MuralSpot m)
    {
        var (rx, rz) = RightOf(m.Facing);
        var (nx, nz) = NormalOf(m.Facing);
        (double, double, double) At(double c, double r) =>
            (m.Origin.X + 0.5 + rx * c + nx * 0.51, m.Origin.Y + 0.5 + r, m.Origin.Z + 0.5 + rz * c + nz * 0.51);
        yield return At((m.Width - 1) / 2.0, (m.Height - 1) / 2.0);
        yield return At(0, 0);
        yield return At(m.Width - 1, 0);
        yield return At(0, m.Height - 1);
        yield return At(m.Width - 1, m.Height - 1);
    }

    static bool TryStamp(SceneContext ctx, BlockPos spawn, MuralSpot spot, HashSet<BlockPos> reach)
    {
        if (!InBounds(ctx.World, spot) || Distance(spawn, spot) < MinDistance || !IsReachable(reach, spot)) return false;
        var backup = Backup(ctx.World, spot);
        Mural.Stamp(ctx.World, ctx.Photo, spot, ctx.Colors.Dark);
        if (IsHidden(ctx.World, spawn, spot)) return true;
        Restore(ctx.World, backup);
        return false;
    }

    /// <summary>A wall two cells from <paramref name="stand"/> toward the spawn, picture facing <paramref name="stand"/>.</summary>
    static MuralSpot? WallFor(SceneContext ctx, BlockPos spawn, BlockPos stand)
    {
        int dx = stand.X - spawn.X, dz = stand.Z - spawn.Z;
        Facing facing = Math.Abs(dx) >= Math.Abs(dz) ? (dx > 0 ? Facing.East : Facing.West) : (dz > 0 ? Facing.South : Facing.North);
        var (nx, nz) = NormalOf(facing);
        var (rx, rz) = RightOf(facing);
        int cx = stand.X - nx * 2, cz = stand.Z - nz * 2;
        var origin = new BlockPos(cx - rx * (FallbackWidth / 2), stand.Y, cz - rz * (FallbackWidth / 2));
        var spot = new MuralSpot(origin, facing, FallbackWidth, FallbackHeight, false);
        return InBounds(ctx.World, spot) ? spot : null;
    }

    /// <summary>
    /// The picture, a stone back one block behind it (so the photo never shows from behind) and a stone plinth under
    /// columns whose ground is lower than the picture's bottom row.
    /// </summary>
    static void BuildWall(SceneContext ctx, MuralSpot spot)
    {
        var w = ctx.World;
        var (rx, rz) = RightOf(spot.Facing);
        var (nx, nz) = NormalOf(spot.Facing);
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        for (int c = 0; c < spot.Width; c++)
            foreach (int back in new[] { 0, 1 })
            {
                int x = spot.Origin.X + rx * c - nx * back, z = spot.Origin.Z + rz * c - nz * back;
                for (int y = spot.Origin.Y - 1; y > 1 && !Block.IsSolid(w.Get(x, y, z)); y--) w.Set(x, y, z, stone);
                if (back == 1)
                    for (int y = spot.Origin.Y; y < spot.Origin.Y + spot.Height; y++)
                        if (!Block.IsOpaque(w.Get(x, y, z))) w.Set(x, y, z, stone);
            }
        Mural.Stamp(w, ctx.Photo, spot, ctx.Colors.Dark);
    }

    static bool InBounds(VoxelWorld w, MuralSpot m)
    {
        var (rx, rz) = RightOf(m.Facing);
        int x1 = m.Origin.X + rx * (m.Width - 1), z1 = m.Origin.Z + rz * (m.Width - 1);
        bool Ok(int x, int z) => x >= 2 && z >= 2 && x < w.SizeX - 2 && z < w.SizeZ - 2;
        return Ok(m.Origin.X, m.Origin.Z) && Ok(x1, z1) && m.Origin.Y >= 2 && m.Origin.Y + m.Height < w.SizeY - 1;
    }

    static double Dist(BlockPos a, BlockPos b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>Every block a stamp or wall may touch (frame, plinth and back included), to undo a rejected spot.</summary>
    static List<(int X, int Y, int Z, uint B)> Backup(VoxelWorld w, MuralSpot m)
    {
        var (rx, rz) = RightOf(m.Facing);
        var (nx, nz) = NormalOf(m.Facing);
        var list = new List<(int, int, int, uint)>();
        for (int c = -1; c <= m.Width; c++)
            foreach (int back in new[] { 0, 1 })
                for (int y = 2; y <= m.Origin.Y + m.Height; y++)
                {
                    int x = m.Origin.X + rx * c - nx * back, z = m.Origin.Z + rz * c - nz * back;
                    list.Add((x, y, z, w.Get(x, y, z)));
                }
        return list;
    }

    static void Restore(VoxelWorld w, List<(int X, int Y, int Z, uint B)> backup)
    {
        foreach (var (x, y, z, b) in backup) w.Set(x, y, z, b);
    }
}
