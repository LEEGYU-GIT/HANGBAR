namespace Hangbar.Core.World.Gen;

/// <summary>
/// Mountain (v2 plan Task 9). One or two peaks with their tops at y 116–136, a 22-block scarp and stepped terraces on
/// the side facing away from the start, a trail (steps of 2 blocks at most) from a flat yard in the south to the
/// summit, a hut you can walk into beside the trail, a cairn by the summit, and one landmark chosen by the seed
/// (ruined tower, stone arch or giant tree). The first view looks from the yard up at the summit; the mural hides
/// in the scarp face.
/// </summary>
public sealed class MountainScene : IScene
{
    const int N = SceneContext.Size;
    const int YardHalf = 4, HutW = 12, HutD = 10;

    public Scene Kind => Scene.Mountain;

    int _px, _pz, _yx, _yz, _sx, _sz, _scarpZ, _scarpX0, _scarpX1;
    readonly List<(int X, int Z)> _trail = new();
    (int X, int Z, int Y)? _hut;
    int _hutSide;

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        var noise = ctx.Noise;
        var h = new double[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                h[z * N + x] = SceneContext.BaseGround + 6 * (noise.Sample(x, z, 40, 11) - 0.5);

        // peaks: the main one in the north half, sometimes a smaller one beside it
        _px = rng.NextInt(70, 123);
        _pz = rng.NextInt(56, 85);
        int top = rng.NextInt(118, 129), radius = rng.NextInt(58, 73);
        Peak(h, noise, _px, _pz, top, radius);
        if (rng.Next() < 0.4)
        {
            int side = rng.Next() < 0.5 ? -1 : 1;
            int qx = Math.Clamp(_px + side * rng.NextInt(30, 46), 50, 142), qz = Math.Clamp(_pz + rng.NextInt(-10, 21), 50, 120);
            Peak(h, noise, qx, qz, top - rng.NextInt(10, 21), (int)(radius * 0.6));
        }

        // the start yard in the south, flat
        _yx = rng.NextInt(70, 123);
        _yz = 150;
        var heights = h.Select(v => (int)Math.Round(v)).ToArray();
        var pins = Enumerable.Repeat(Slope.NoPin, N * N).ToArray();
        int yardY = heights[_yz * N + _yx];
        ForBox(_yx - YardHalf, _yz - YardHalf, _yx + YardHalf, _yz + YardHalf, i => { pins[i] = yardY; ctx.NoCaves[i] = true; });
        heights = Slope.Limit(heights, pins, N, 2);

        // cliffs on the side facing away from the start: a scarp north of the main peak and terraces on one flank
        _scarpZ = _pz - rng.NextInt(18, 27);
        _scarpX0 = _px - 30;
        _scarpX1 = _px + 30;
        for (int z = 0; z < _scarpZ; z++)
            for (int x = Math.Max(0, _scarpX0); x <= Math.Min(N - 1, _scarpX1); x++)
            {
                double tx = Math.Clamp((30 - Math.Abs(x - _px)) / 6.0, 0, 1);
                double tz = Math.Clamp((z - (_scarpZ - 32)) / 12.0, 0, 1);
                heights[z * N + x] -= (int)Math.Round(22 * tx * tz);
            }
        int flank = rng.Next() < 0.5 ? -1 : 1;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                double dx = x - _px, dz = z - _pz, d = Math.Sqrt(dx * dx + dz * dz) / radius;
                double angle = Math.Atan2(dz, dx * flank); // 0 = straight out on the flank side
                if (d < 0.25 || d > 0.75 || Math.Abs(angle) > Math.PI / 6 || dz > 0) continue;
                int i = z * N + x, above = heights[i] - SceneContext.BaseGround;
                heights[i] = SceneContext.BaseGround + above / 7 * 7;
            }

        Array.Copy(heights, ctx.Surface, heights.Length);
        int best = 0;
        for (int i = 1; i < N * N; i++) if (ctx.Surface[i] > ctx.Surface[best]) best = i;
        _sx = best % N;
        _sz = best / N;

        _trail.AddRange(FindTrail(ctx.Surface, (_yx, _yz), (_sx, _sz)) ?? new List<(int, int)>());
        PlaceHutPlot(ctx);
        foreach (var (x, z) in _trail)
            ForBox(x - 1, z - 1, x + 1, z + 1, i => ctx.Path[i] = true);
        Materials(ctx);
    }

    static void Peak(double[] h, ValueNoise noise, int px, int pz, int top, int radius)
    {
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                double d = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)) / radius;
                if (d >= 1) continue;
                double ridge = 1 + 0.3 * (noise.Sample(x, z, 14, 21) - 0.5) * d;
                double v = SceneContext.BaseGround + (top - SceneContext.BaseGround) * Math.Pow(1 - d, 1.6) * ridge;
                h[z * N + x] = Math.Max(h[z * N + x], v);
            }
    }

    /// <summary>A* over columns, steps of at most 2 blocks, gentle steps cheaper.</summary>
    static List<(int X, int Z)>? FindTrail(int[] s, (int X, int Z) from, (int X, int Z) to, Func<int, int, bool>? blocked = null)
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
                int j = nz * N + nx, rise = Math.Abs(s[j] - s[i]);
                if (rise > 2) continue;
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

    /// <summary>A flat plot for the hut beside the trail, on the side away from the spawn–summit line of sight.</summary>
    void PlaceHutPlot(SceneContext ctx)
    {
        if (_trail.Count < 20) return;
        double lx = _sx - _yx, lz = _sz - _yz;
        foreach (double share in new[] { 0.45, 0.35, 0.55, 0.3, 0.6 })
        {
            var (tx, tz) = _trail[(int)(_trail.Count * share)];
            int side = (tx - _yx) * lz - (tz - _yz) * lx >= 0 ? 1 : -1; // which side of the sight line the trail is on
            int x0 = side > 0 ? tx + 4 : tx - 4 - HutD, z0 = tz - HutW / 2;
            if (x0 < 20 || z0 < 20 || x0 + HutD > N - 20 || z0 + HutW > N - 20) continue;
            int y = ctx.Surface[tz * N + tx];
            var trial = (int[])ctx.Surface.Clone();
            int wx0 = Math.Max(0, x0 - 20), wz0 = Math.Max(0, z0 - 20), wx1 = Math.Min(N - 1, x0 + HutD + 20), wz1 = Math.Min(N - 1, z0 + HutW + 20);
            int ww = wx1 - wx0 + 1, wd = wz1 - wz0 + 1, size = Math.Max(ww, wd);
            var local = new int[size * size];
            var pins = Enumerable.Repeat(Slope.NoPin, size * size).ToArray();
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    int gx = Math.Min(wx0 + x, N - 1), gz = Math.Min(wz0 + z, N - 1);
                    local[z * size + x] = trial[gz * N + gx];
                    bool plot = gx >= x0 - 2 && gx < x0 + HutD + 2 && gz >= z0 - 2 && gz < z0 + HutW + 2;
                    if (plot) pins[z * size + x] = y;
                    else if (x >= ww || z >= wd || x == 0 || z == 0 || x == ww - 1 || z == wd - 1)
                        pins[z * size + x] = local[z * size + x]; // keep the window's border: the rest of the map is untouched
                }
            int[] limited;
            try { limited = Slope.Limit(local, pins, size, 2); }
            catch (InvalidOperationException) { continue; }
            for (int z = 0; z < wd; z++)
                for (int x = 0; x < ww; x++) ctx.Surface[(wz0 + z) * N + wx0 + x] = limited[z * size + x];
            // the ground around the hut moved: find the trail again, around the hut
            bool Blocked(int x, int z) => x >= x0 - 1 && x <= x0 + HutD && z >= z0 - 1 && z <= z0 + HutW;
            var trail = FindTrail(ctx.Surface, (_yx, _yz), (_sx, _sz), Blocked);
            if (trail is null)
            {
                Array.Copy(trial, ctx.Surface, trial.Length);
                continue;
            }
            _trail.Clear();
            _trail.AddRange(trail);
            ForBox(x0 - 2, z0 - 2, x0 + HutD + 1, z0 + HutW + 1, i => ctx.NoCaves[i] = true);
            _hut = (x0, z0, y);
            _hutSide = side;
            return;
        }
    }

    void Materials(SceneContext ctx)
    {
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int i = z * N + x, top = ctx.Surface[i] - 1, steep = 0;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < N && nz < N) steep = Math.Max(steep, Math.Abs(ctx.Surface[nz * N + nx] - ctx.Surface[i]));
                }
                ctx.TopMaterial[i] = top >= 128 ? Material.Snow
                    : ctx.Path[i] ? Material.Soil
                    : steep >= 3 || top >= 116 ? Material.Stone
                    : Material.Grass;
            }
    }

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        if (_hut is { } hut)
        {
            var placed = PartPlacer.Place(w, PartLibrary.Get("mountain_hut"), new BlockPos(hut.X, hut.Y, hut.Z),
                _hutSide > 0 ? Rotation.R270 : Rotation.R90, ctx.Colors, new PlaceOptions(), ctx.Rng); // door toward the trail
            ctx.Structures.Add(placed);
        }
        // the cairn stands just north of the summit, behind it as seen from the start
        int cx = Math.Clamp(_sx - 1, 1, N - 4), cz = Math.Clamp(_sz - 5, 1, N - 4);
        int cy = Enumerable.Range(0, 9).Max(k => ctx.Surface[(cz + k / 3) * N + cx + k % 3]);
        Support(ctx, cx, cz, 3, 3, cy);
        ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("summit_cairn"), new BlockPos(cx, cy, cz), Rotation.R0, ctx.Colors, new PlaceOptions(), ctx.Rng));

        Landmark(ctx);
        Props(ctx);
    }

    /// <summary>Fills air under a footprint up to y (exclusive) so a structure does not float.</summary>
    static void Support(SceneContext ctx, int x0, int z0, int w, int d, int y)
    {
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        for (int z = z0; z < z0 + d; z++)
            for (int x = x0; x < x0 + w; x++)
                for (int yy = y - 1; yy > 1 && !Block.IsSolid(ctx.World.Get(x, yy, z)); yy--) ctx.World.Set(x, yy, z, stone);
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        int pick = ctx.Rng.NextInt(0, 3), flank = _px < N / 2 ? 1 : -1;
        int lx = Math.Clamp(_px + flank * 48, 30, N - 40), lz = Math.Clamp(_pz + 30, 40, 120);
        for (int k = 0; k < 20 && ctx.Path[lz * N + lx]; k++) lx = Math.Clamp(lx + flank * 4, 30, N - 40);
        int ground = Ground(ctx, lx, lz, 12, 12);
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        switch (pick)
        {
            case 0:
            {
                Support(ctx, lx, lz, 7, 7, ground);
                var tower = PartPlacer.Place(w, PartLibrary.Get("ruined_tower"), new BlockPos(lx, ground, lz), Rotation.R0, ctx.Colors,
                    new PlaceOptions(Missing: 0.2), ctx.Rng);
                ctx.Structures.Add(tower);
                ctx.MarkOld(tower);
                ctx.Landmark = new Landmark("ruined_tower", new BlockPos(lx + 3, ground, lz + 3));
                break;
            }
            case 1:
            {
                Support(ctx, lx, lz, 13, 3, ground);
                for (int y = ground; y < ground + 15; y++)
                    for (int z = lz; z < lz + 3; z++)
                        for (int x = lx; x < lx + 13; x++)
                        {
                            bool pillar = x < lx + 3 || x >= lx + 10, lintel = y >= ground + 12;
                            if (pillar || lintel) w.Set(x, y, z, stone);
                        }
                ctx.Structures.Add(new PlacedPart("stone_arch", new BlockPos(lx, ground, lz), new BlockPos(lx + 12, ground + 14, lz + 2),
                    new Dictionary<string, BlockPos>()));
                ctx.Landmark = new Landmark("stone_arch", new BlockPos(lx + 6, ground, lz + 1));
                break;
            }
            default:
            {
                uint wood = Block.Solid(ctx.ColorOf(Material.Wood), Material.Wood);
                Support(ctx, lx, lz, 5, 5, ground);
                for (int y = ground; y < ground + 2; y++)
                    for (int z = lz; z < lz + 5; z++)
                        for (int x = lx; x < lx + 5; x++)
                            if (!((x == lx || x == lx + 4) && (z == lz || z == lz + 4))) w.Set(x, y, z, wood);
                ctx.Structures.Add(new PlacedPart("giant_tree_roots", new BlockPos(lx, ground, lz), new BlockPos(lx + 4, ground + 1, lz + 4),
                    new Dictionary<string, BlockPos>()));
                ctx.Props.Add(new Prop(PropKind.Tree, lx + 2, lz + 2, 4.5f, ctx.Colors.Accent));
                ctx.Landmark = new Landmark("giant_tree", new BlockPos(lx + 2, ground + 2, lz + 2));
                break;
            }
        }
    }

    static int Ground(SceneContext ctx, int x0, int z0, int w, int d)
    {
        int y = 0;
        for (int z = z0; z < z0 + d; z++)
            for (int x = x0; x < x0 + w; x++) y = Math.Max(y, ctx.Surface[z * N + x]);
        return y;
    }

    void Props(SceneContext ctx)
    {
        bool Covered(int x, int z) => ctx.Structures.Any(s => x >= s.Min.X - 1 && x <= s.Max.X + 1 && z >= s.Min.Z - 1 && z <= s.Max.Z + 1);
        for (int z = 2; z < N - 2; z++)
            for (int x = 2; x < N - 2; x++)
            {
                int i = z * N + x;
                if (ctx.Path[i] || ctx.NoCaves[i] || Covered(x, z)) continue;
                double r = ctx.Noise.Hash01(x, z, 77);
                int top = Top(ctx.World, x, z);
                if (top < 0 || !Block.IsSolid(ctx.World.Get(x, top, z))) continue;
                var mat = Block.MaterialOf(ctx.World.Get(x, top, z));
                if (mat == Material.Grass && top < 110 && r < 1.0 / 80)
                    ctx.Props.Add(new Prop(PropKind.Tree, x, z, (float)(0.8 + 0.5 * ctx.Noise.Hash01(x, z, 78)), ctx.Colors.Accent));
                else if (mat == Material.Stone && r < 1.0 / 300)
                    ctx.Props.Add(new Prop(PropKind.Rock, x, z, (float)(0.6 + 0.6 * ctx.Noise.Hash01(x, z, 79)), ctx.ColorOf(Material.Stone)));
            }
    }

    static int Top(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (w.Get(x, y, z) != Block.Air) return y;
        return -1;
    }

    public SceneView View(SceneContext ctx)
    {
        var spawn = new BlockPos(_yx, ctx.Surface[_yz * N + _yx], _yz);
        var target = new BlockPos(_sx, ctx.Surface[_sz * N + _sx], _sz);
        var candidates = new List<MuralSpot>();
        // the scarp face north of the main peak: the picture faces north, out of the face
        int width = 32, x0 = _px - width / 2, right = x0 + width - 1, z = _scarpZ;
        if (x0 > 18 && right < N - 18 && z > 18)
        {
            int low = 0, high = int.MaxValue;
            for (int x = x0; x <= right; x++)
            {
                low = Math.Max(low, ctx.Surface[(z - 1) * N + x]);
                high = Math.Min(high, ctx.Surface[z * N + x]);
            }
            int height = Math.Min(18, high - low - 1);
            if (height >= 10) candidates.Add(new MuralSpot(new BlockPos(right, low, z), Facing.North, width, height, false));
        }
        return new SceneView(spawn, target, candidates);
    }

    static void ForBox(int x0, int z0, int x1, int z1, Action<int> f)
    {
        for (int z = Math.Max(0, z0); z <= Math.Min(N - 1, z1); z++)
            for (int x = Math.Max(0, x0); x <= Math.Min(N - 1, x1); x++) f(z * N + x);
    }
}
