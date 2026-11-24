namespace Hangbar.Core.World.Gen;

/// <summary>
/// Where a 1.75 m body can stand and walk: solid block underneath, 7 free blocks above, side steps of at most 2 blocks.
/// Water is not solid, so a body walks on the bottom of shallow water (as in v1).
/// </summary>
public static class Walkability
{
    public const int Step = 2, Headroom = 7;

    public static bool IsStandable(VoxelWorld w, int x, int y, int z)
    {
        if (y <= 0 || !Block.IsSolid(w.Get(x, y - 1, z))) return false;
        for (int k = 0; k < Headroom; k++)
            if (Block.IsSolid(w.Get(x, y + k, z))) return false;
        return true;
    }

    /// <summary>Every standable cell reached from <paramref name="start"/>; empty when the start itself is not standable.</summary>
    public static HashSet<BlockPos> ReachCells(VoxelWorld w, BlockPos start)
    {
        var seen = new HashSet<BlockPos>();
        if (!IsStandable(w, start.X, start.Y, start.Z)) return seen;
        var columns = new List<int>?[w.SizeX * w.SizeZ];
        List<int> Standable(int x, int z)
        {
            int i = z * w.SizeX + x;
            if (columns[i] is { } known) return known;
            var list = new List<int>();
            for (int y = 1; y < w.SizeY; y++)
                if (IsStandable(w, x, y, z)) list.Add(y);
            columns[i] = list;
            return list;
        }
        var queue = new Queue<BlockPos>();
        seen.Add(start);
        queue.Enqueue(start);
        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var (dx, dz) in steps)
            {
                int nx = p.X + dx, nz = p.Z + dz;
                if (nx < 0 || nz < 0 || nx >= w.SizeX || nz >= w.SizeZ) continue;
                foreach (int ny in Standable(nx, nz))
                {
                    if (Math.Abs(ny - p.Y) > Step) continue;
                    var q = new BlockPos(nx, ny, nz);
                    if (seen.Add(q)) queue.Enqueue(q);
                }
            }
        }
        return seen;
    }

    /// <summary>Per column (z·SizeX + x): true when any cell of that column is reached.</summary>
    public static bool[] ReachColumns(VoxelWorld w, BlockPos start)
    {
        var r = new bool[w.SizeX * w.SizeZ];
        foreach (var p in ReachCells(w, start)) r[p.Z * w.SizeX + p.X] = true;
        return r;
    }
}
