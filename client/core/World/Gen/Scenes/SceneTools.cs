namespace Hangbar.Core.World.Gen;

/// <summary>Helpers the part-2 scenes share (v2 part 2 plan Task 1). World coordinates unless a frame is given.</summary>
public static class SceneTools
{
    const int N = SceneContext.Size;

    /// <summary>A* over columns: side steps of at most <paramref name="step"/> blocks, gentle steps cheaper.</summary>
    public static List<(int X, int Z)>? FindPath(int[] surface, (int X, int Z) from, (int X, int Z) to,
        Func<int, int, bool>? blocked = null, int step = 2)
    {
        var cost = new double[N * N];
        Array.Fill(cost, double.MaxValue);
        var prev = new int[N * N];
        var open = new PriorityQueue<int, double>();
        int start = from.Z * N + from.X, goal = to.Z * N + to.X;
        cost[start] = 0;
        open.Enqueue(start, 0);
        while (open.TryDequeue(out int i, out _))
        {
            if (i == goal) break;
            int x = i % N, z = i / N;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 1 || nz < 1 || nx >= N - 1 || nz >= N - 1 || blocked?.Invoke(nx, nz) == true) continue;
                int j = nz * N + nx, rise = Math.Abs(surface[j] - surface[i]);
                if (rise > step) continue;
                double c = cost[i] + 1 + 0.6 * rise;
                if (c >= cost[j]) continue;
                cost[j] = c;
                prev[j] = i;
                open.Enqueue(j, c + Math.Abs(nx - to.X) + Math.Abs(nz - to.Z));
            }
        }
        if (cost[goal] == double.MaxValue) return null;
        var path = new List<(int, int)>();
        for (int i = goal; ; i = prev[i])
        {
            path.Add((i % N, i / N));
            if (i == start) break;
        }
        path.Reverse();
        return path;
    }

    /// <summary>Noise hills: base + amp × smooth noise (0..1) of the given cell size, into ctx.Surface.</summary>
    public static void Hills(SceneContext ctx, double baseY, double amp, int cell, uint salt)
    {
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                ctx.Surface[z * N + x] = (int)Math.Round(baseY + amp * ctx.Noise.Sample(x, z, cell, salt));
    }

    /// <summary>First free cell above the highest solid block of a column.</summary>
    public static int TopFree(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }

    /// <summary>Fills air under a footprint (world box x0..x0+wx−1, z0..z0+wz−1) below y, so a structure does not float.</summary>
    public static void Support(VoxelWorld w, int x0, int z0, int wx, int wz, int y, uint block)
    {
        for (int z = z0; z < z0 + wz; z++)
            for (int x = x0; x < x0 + wx; x++)
                for (int yy = y - 1; yy > 1 && !Block.IsSolid(w.Get(x, yy, z)); yy--) w.Set(x, yy, z, block);
    }

    /// <summary>Highest ctx.Surface over a local box.</summary>
    public static int MaxSurface(SceneContext ctx, LocalFrame f, int a0, int b0, int wa, int wb)
    {
        int y = 0;
        for (int b = b0; b < b0 + wb; b++)
            for (int a = a0; a < a0 + wa; a++) y = Math.Max(y, ctx.Surface[f.I(a, b)]);
        return y;
    }

    public static int MinSurface(SceneContext ctx, LocalFrame f, int a0, int b0, int wa, int wb)
    {
        int y = int.MaxValue;
        for (int b = b0; b < b0 + wb; b++)
            for (int a = a0; a < a0 + wa; a++) y = Math.Min(y, ctx.Surface[f.I(a, b)]);
        return y;
    }

    /// <summary>Places a part and records it as a structure.</summary>
    public static PlacedPart Place(SceneContext ctx, string part, BlockPos origin, Rotation r, PlaceOptions? options = null)
    {
        var placed = PartPlacer.Place(ctx.World, PartLibrary.Get(part), origin, r, ctx.Colors, options ?? new PlaceOptions(), ctx.Rng);
        ctx.Structures.Add(placed);
        return placed;
    }

    /// <summary>
    /// A fence round the local box a0..a1 × b0..b1 (lines inclusive) at height y. Sides: 0 the b0 row, 1 the b1 row,
    /// 2 the a0 column, 3 the a1 column. Each gate (side, centre, width) leaves that stretch open.
    /// </summary>
    public static void FenceRing(SceneContext ctx, LocalFrame f, int a0, int b0, int a1, int b1, int y,
        params (int Side, int Center, int Width)[] gates)
    {
        void Run(int from, int to, bool alongA, int at)
        {
            int len = to - from;
            if (len < 2) return;
            var origin = alongA ? f.Origin(from, at, len, 1, y) : f.Origin(at, from, 1, len, y);
            Place(ctx, "fence_segment", origin, LocalFrame.Along(alongA ? f.FacingOf(1, 0) : f.FacingOf(0, 1)), new PlaceOptions(StretchTo: len));
        }
        void Side(int side, int from, int to, bool alongA, int at)
        {
            var cuts = gates.Where(g => g.Side == side).Select(g => (Lo: g.Center - g.Width / 2, Hi: g.Center - g.Width / 2 + g.Width))
                .OrderBy(g => g.Lo).ToList();
            int start = from;
            foreach (var (lo, hi) in cuts)
            {
                Run(start, Math.Clamp(lo, from, to), alongA, at);
                start = Math.Clamp(hi, from, to);
            }
            Run(start, to, alongA, at);
        }
        Side(0, a0, a1 + 1, true, b0);
        Side(1, a0, a1 + 1, true, b1);
        Side(2, b0 + 1, b1, false, a0);
        Side(3, b0 + 1, b1, false, a1);
    }

    /// <summary>
    /// Scatters props: for every column off paths and clear of structures (by <paramref name="margin"/>), with a solid
    /// top and air above, asks <paramref name="pick"/> (x, z, top material, a 0..1 hash) for a prop.
    /// </summary>
    public static void Scatter(SceneContext ctx, Func<int, int, Material, double, Prop?> pick, int margin = 1)
    {
        var w = ctx.World;
        var boxes = ctx.Structures.Select(s => (s.Min.X - margin, s.Max.X + margin, s.Min.Z - margin, s.Max.Z + margin)).ToArray();
        for (int z = 2; z < N - 2; z++)
            for (int x = 2; x < N - 2; x++)
            {
                if (ctx.Path[z * N + x]) continue;
                bool covered = false;
                foreach (var (x0, x1, z0, z1) in boxes)
                    if (x >= x0 && x <= x1 && z >= z0 && z <= z1) { covered = true; break; }
                if (covered) continue;
                int top = TopFree(w, x, z);
                if (top < 2 || Block.IsWater(w.Get(x, top, z)) || w.Get(x, top, z) != Block.Air) continue;
                if (pick(x, z, Block.MaterialOf(w.Get(x, top - 1, z)), ctx.Noise.Hash01(x, z, 77)) is { } p) ctx.Props.Add(p);
            }
    }
}
