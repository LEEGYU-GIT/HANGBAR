namespace Hangbar.Core.World.Gen;

/// <summary>
/// Country road (v2 part 2 plan Task 4; the Road biome's default). A two-lane asphalt road winds across the map from
/// edge to edge between gentle hills, its height changing at most one block per step, gravel shoulders on both sides
/// (paths), rows of trees along it, a bus stop across the road, a farmhouse shell and stone walls by the fields. The
/// landmark is an old stone bridge over a creek, a small chapel or a signpost under a big tree. The first view looks
/// from the shoulder along the road.
/// </summary>
/// <remarks>Local frame: a runs along the road, b across it; the road's centre is b = Centre(a).</remarks>
public sealed class CountryRoadScene : IScene
{
    const int N = SceneContext.Size, Half = 12, Shoulder = 3;

    public Scene Kind => Scene.CountryRoad;

    LocalFrame _f;
    double _amp, _waves, _phase, _mid;
    int _sA, _stopA, _houseA, _creekA, _lA, _landmark;
    readonly int[] _roadY = new int[N];

    int Centre(int a) => (int)Math.Round(_mid + _amp * Math.Sin(2 * Math.PI * _waves * a / N + _phase));

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _amp = 14 + 14 * rng.Next(); _waves = 0.6 + 0.6 * rng.Next(); _phase = rng.Next() * 2 * Math.PI; _mid = rng.NextInt(86, 107);
        _landmark = rng.NextInt(0, 3);
        _sA = rng.NextInt(30, 46);
        _stopA = _sA + rng.NextInt(55, 70);
        _houseA = rng.NextInt(110, 150);
        _creekA = rng.NextInt(120, 150);
        _lA = _landmark == 0 ? _creekA : rng.NextInt(125, 155);

        var h = new int[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                h[z * N + x] = (int)Math.Round(56 + 10 * ctx.Noise.Sample(x, z, 60, 201) + 2 * ctx.Noise.Sample(x, z, 16, 202));

        // the road's height along its length: the ground under its centre, changing one block per step at most
        for (int a = 0; a < N; a++) _roadY[a] = h[_f.I(a, Math.Clamp(Centre(a), 0, N - 1))];
        for (int a = 1; a < N; a++) _roadY[a] = Math.Clamp(_roadY[a], _roadY[a - 1] - 1, _roadY[a - 1] + 1);
        for (int a = N - 2; a >= 0; a--) _roadY[a] = Math.Clamp(_roadY[a], _roadY[a + 1] - 1, _roadY[a + 1] + 1);

        var pins = Enumerable.Repeat(Slope.NoPin, N * N).ToArray();
        for (int a = 0; a < N; a++)
        {
            int c = Centre(a);
            for (int b = c - Half - Shoulder - 1; b <= c + Half + Shoulder; b++)
                if (b >= 0 && b < N) pins[_f.I(a, b)] = _roadY[a];
        }
        var s = Slope.Limit(h, pins, N, 2);
        Array.Copy(s, ctx.Surface, s.Length);

        // flat plots for the bus stop, the farmhouse and the landmark, levelled after the slope limit (pinning them
        // next to a road that climbs would make the limits conflict); a few blocks of bank at their edges are fine
        void Flatten(int a0, int b0, int wa, int wb)
        {
            int y = ctx.Surface[_f.I(a0 + wa / 2, b0 + wb / 2)];
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++)
                {
                    int i = _f.I(a, b), c = Centre(a);
                    if (b >= c - Half - Shoulder - 1 && b <= c + Half + Shoulder) continue;
                    ctx.Surface[i] = y;
                    ctx.NoCaves[i] = true;
                }
        }
        Flatten(_stopA - 2, Centre(_stopA) + Half + Shoulder + 1, 16, 10);
        Flatten(_houseA - 2, Centre(_houseA) - Half - Shoulder - 24, 20, 18);
        if (_landmark != 0) Flatten(_lA - 10, Centre(_lA) + Half + Shoulder + 14, 24, 26);

        for (int a = 0; a < N; a++)
        {
            int c = Centre(a);
            for (int b = c - Half - Shoulder; b < c + Half + Shoulder; b++)
            {
                if (b < 0 || b >= N) continue;
                int i = _f.I(a, b), off = b - c;
                ctx.Path[i] = true;
                ctx.NoCaves[i] = true;
                ctx.TopMaterial[i] = off < -Half || off >= Half ? Material.Soil
                    : (off == 0 || off == -1) && (a / 8) % 2 == 0 ? Material.Tile
                    : Material.Concrete;
            }
        }

        // the creek crosses the road under a culvert (the stone bridge when it is the landmark)
        // the water follows the ground: one block below the lower bank of each row, two blocks deep
        if (_landmark == 0)
            for (int b = 0; b < N; b++)
            {
                int bank = Math.Min(ctx.Surface[_f.I(_creekA - 5, b)], ctx.Surface[_f.I(_creekA + 4, b)]);
                for (int a = _creekA - 4; a < _creekA + 4; a++)
                {
                    int i = _f.I(a, b), c = Centre(a);
                    if (b >= c - Half - Shoulder - 1 && b <= c + Half + Shoulder) continue; // the road stays solid over it
                    ctx.Surface[i] = Math.Min(ctx.Surface[i], bank - 3);
                    ctx.WaterTop[i] = bank - 1;
                    ctx.TopMaterial[i] = Material.Sand;
                    ctx.NoCaves[i] = true;
                }
            }
    }

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        int stopB = Centre(_stopA) + Half + Shoulder + 2;
        // the bus stop faces the road across from the start: its open front toward −b
        SceneTools.Place(ctx, "bus_stop", _f.Origin(_stopA, stopB, 12, 5, ctx.Surface[_f.I(_stopA + 6, stopB + 2)]), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
        int houseB = Centre(_houseA) - Half - Shoulder - 22;
        SceneTools.Place(ctx, "house_shell", _f.Origin(_houseA, houseB, 16, 12, SceneTools.MaxSurface(ctx, _f, _houseA, houseB, 16, 12)),
            LocalFrame.FrontTo(_f.FacingOf(0, 1)));

        // stone walls along the fields on both sides, broken here and there
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        foreach (int side in new[] { -1, 1 })
            for (int a = 4; a < N - 4; a++)
            {
                if (ctx.Noise.Hash01(a, side, 41) < 0.25 || (a / 24) % 3 == 2) continue;
                int b = Centre(a) + side * (Half + Shoulder + 8);
                if (b < 4 || b >= N - 4 || ctx.WaterTop[_f.I(a, b)] > 0 || Math.Abs(a - _stopA - 6) < 10 || Math.Abs(a - _houseA - 8) < 12) continue;
                var (x, z) = _f.W(a, b);
                int y = ctx.Surface[_f.I(a, b)];
                w.Set(x, y, z, stone);
                w.Set(x, y + 1, z, stone);
            }

        // rows of trees along the road, every 9 to 13 cells, on both sides
        foreach (int side in new[] { -1, 1 })
            for (int a = 6; a < N - 6; a += 9 + (int)(4 * ctx.Noise.Hash01(a, side, 42)))
            {
                int b = Centre(a) + side * (Half + Shoulder + 4);
                if (b < 2 || b >= N - 2) continue;
                var (x, z) = _f.W(a, b);
                if (ctx.WaterTop[z * N + x] > 0 || Math.Abs(a - _stopA - 6) < 9) continue;
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, (float)(1.0 + 0.4 * ctx.Noise.Hash01(a, b, 43)), ctx.Colors.Accent));
            }

        Landmark(ctx);
        SceneTools.Scatter(ctx, (x, z, mat, r) =>
        {
            float size = (float)(0.6 + 0.5 * ctx.Noise.Hash01(x, z, 78));
            if (mat == Material.Grass && r < 1.0 / 45) return new Prop(PropKind.Plant, x, z, size * 0.6f, ctx.Colors.Light);
            if (mat == Material.Grass && r is >= 0.5 and < 0.5 + 1.0 / 160) return new Prop(PropKind.Bush, x, z, size, ctx.Colors.Accent);
            if (r is >= 0.8 and < 0.8 + 1.0 / 300) return new Prop(PropKind.Rock, x, z, size, ctx.ColorOf(Material.Stone));
            return null;
        }, margin: 2);
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        switch (_landmark)
        {
            case 0:
            {
                // stone parapets on both edges of the road over the creek
                int c = Centre(_creekA), y = _roadY[_creekA];
                foreach (int edge in new[] { c - Half - Shoulder - 1, c + Half + Shoulder })
                    for (int a = _creekA - 7; a < _creekA + 7; a++)
                        for (int k = 0; k < 3; k++)
                        {
                            var (x, z) = _f.W(a, edge);
                            w.Set(x, y + k, z, stone);
                        }
                var bridge = _f.Box("stone_bridge", _creekA - 7, c - Half - Shoulder - 1, 14, 2 * (Half + Shoulder) + 2, y - 4, y + 2);
                ctx.Structures.Add(bridge);
                ctx.MarkOld(bridge);
                ctx.Landmark = new Landmark("stone_bridge", Center(_creekA, c, y + 2));
                break;
            }
            case 1:
            {
                int b0 = Centre(_lA) + Half + Shoulder + 16, y = SceneTools.MaxSurface(ctx, _f, _lA - 7, b0, 14, 20);
                SceneTools.Place(ctx, "chapel_shell", _f.Origin(_lA - 7, b0, 14, 20, y), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
                ctx.Landmark = new Landmark("chapel", Center(_lA, b0 + 2, y + 20));
                break;
            }
            default:
            {
                int b = Centre(_lA) + Half + Shoulder + 14, y = SceneTools.MaxSurface(ctx, _f, _lA - 1, b - 1, 3, 3);
                var (x, z) = _f.W(_lA, b);
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, 4f, ctx.Colors.Accent));
                SceneTools.Place(ctx, "signpost", _f.Origin(_lA - 4, b - 8, 9, 1, ctx.Surface[_f.I(_lA, b - 8)]), LocalFrame.Along(_f.FacingOf(1, 0)));
                ctx.Landmark = new Landmark("signpost_tree", Center(_lA, b, y + 10));
                break;
            }
        }
    }

    BlockPos Center(int a, int b, int y)
    {
        var (x, z) = _f.W(a, b);
        return new BlockPos(x, y, z);
    }

    public SceneView View(SceneContext ctx)
    {
        int sb = Centre(_sA) - Half - 2;
        var (sx, sz) = _f.W(_sA, sb);
        var spawn = new BlockPos(sx, ctx.Surface[sz * N + sx], sz);
        int ta = _sA + 40;
        var target = Center(ta, Centre(ta), _roadY[ta] + 6);
        var away = _f.FacingOf(0, 1);
        int stopB = Centre(_stopA) + Half + Shoulder + 2, stopY = ctx.Surface[_f.I(_stopA + 6, stopB + 2)];
        var candidates = new List<MuralSpot>
        {
            // the bus stop's back wall, facing the fields away from the road
            LocalFrame.Spot(_f.W(_stopA + 1, stopB + 4), _f.W(_stopA + 10, stopB + 4), stopY + 1, away, 9),
        };
        int houseB = Centre(_houseA) - Half - Shoulder - 22, houseY = SceneTools.MaxSurface(ctx, _f, _houseA, houseB, 16, 12);
        // the farmhouse's back wall, facing away from the road (its front faces +b)
        candidates.Add(LocalFrame.Spot(_f.W(_houseA + 1, houseB), _f.W(_houseA + 14, houseB), houseY + 1, _f.FacingOf(0, -1), 9));
        return new SceneView(spawn, target, candidates);
    }
}
