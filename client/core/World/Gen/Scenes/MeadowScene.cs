namespace Hangbar.Core.World.Gen;

/// <summary>
/// Meadow (v2 part 2 plan Task 2; the Field biome's default). Rolling hills limited to steps of two, a pond, a dirt
/// path looping round the middle, flowers, a few trees, bushes and rocks, a closed shed and bits of fence. The landmark
/// is a windmill, a lone giant tree or a stone circle, across the meadow from the start on a hill. The mural hides in
/// the far face of a rock outcrop or on the shed's back wall.
/// </summary>
/// <remarks>Local frame: b runs from the start (small b) to the landmark (large b).</remarks>
public sealed class MeadowScene : IScene
{
    const int N = SceneContext.Size;
    const int OutW = 26, OutD = 5, OutRise = 14, MuralW = 20, MuralH = 11;

    public Scene Kind => Scene.Meadow;

    LocalFrame _f;
    int _sA, _sB, _lA, _lB, _landmark, _oA, _oB, _oY, _shedA, _shedB, _pondA, _pondB, _pondR;

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _landmark = rng.NextInt(0, 3);
        _sA = rng.NextInt(70, 123); _sB = 36;
        _lA = rng.NextInt(60, 133); _lB = rng.NextInt(132, 146);
        bool outLeft = rng.Next() < 0.5;
        _oA = outLeft ? rng.NextInt(24, 40) : rng.NextInt(N - 40 - OutW, N - 24 - OutW);
        _oB = rng.NextInt(88, 104);
        _shedA = outLeft ? rng.NextInt(N - 60, N - 40) : rng.NextInt(30, 50);
        _shedB = rng.NextInt(70, 120);
        _pondA = rng.NextInt(60, 133); _pondB = rng.NextInt(64, 84); _pondR = rng.NextInt(9, 14);

        // rolling hills: a broad swell and a finer ripple
        var h = new int[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                h[z * N + x] = (int)Math.Round(56 + 13 * ctx.Noise.Sample(x, z, 56, 101) + 3 * ctx.Noise.Sample(x, z, 14, 102));

        // the start: the highest ground on the near side, so the view runs over the meadow
        int best = -1;
        for (int b = 26; b <= 48; b++)
            for (int a = 50; a <= 142; a++)
                if (h[_f.I(a, b)] > best) { best = h[_f.I(a, b)]; _sA = a; _sB = b; }

        // flat pads: the start hill, the landmark, the outcrop's foot, the shed
        var pins = Enumerable.Repeat(Slope.NoPin, N * N).ToArray();
        void Pad(int a0, int b0, int wa, int wb)
        {
            int y = h[_f.I(a0 + wa / 2, b0 + wb / 2)];
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++) { pins[_f.I(a, b)] = y; ctx.NoCaves[_f.I(a, b)] = true; }
        }
        Pad(_sA - 3, _sB - 3, 7, 7);
        Pad(_lA - 12, _lB - 12, 24, 24);
        Pad(_oA - 2, _oB - 2, OutW + 4, OutD + 6);
        Pad(_shedA - 2, _shedB - 2, 14, 12);
        var s = Slope.Limit(h, pins, N, 2);
        Array.Copy(s, ctx.Surface, s.Length);

        // the pond: a bowl below the lowest rim around it
        int rim = int.MaxValue;
        for (int k = 0; k < 64; k++)
        {
            double t = k * Math.PI / 32;
            int a = _pondA + (int)Math.Round(Math.Cos(t) * (_pondR + 2)), b = _pondB + (int)Math.Round(Math.Sin(t) * (_pondR + 2));
            rim = Math.Min(rim, ctx.Surface[_f.I(a, b)]);
        }
        for (int b = _pondB - _pondR; b <= _pondB + _pondR; b++)
            for (int a = _pondA - _pondR; a <= _pondA + _pondR; a++)
            {
                double d = Math.Sqrt((a - _pondA) * (a - _pondA) + (b - _pondB) * (b - _pondB)) / _pondR;
                if (d > 1) continue;
                int i = _f.I(a, b);
                ctx.Surface[i] = Math.Min(ctx.Surface[i], rim - 1 - (int)Math.Round(3 * (1 - d * d)));
                ctx.WaterTop[i] = rim;
                ctx.TopMaterial[i] = Material.Sand;
                ctx.NoCaves[i] = true;
            }

        // the outcrop stands up after the slope limit: a cliff, its far face the mural wall
        _oY = ctx.Surface[_f.I(_oA + OutW / 2, _oB + OutD + 1)];
        for (int b = _oB; b < _oB + OutD; b++)
            for (int a = _oA; a < _oA + OutW; a++)
            {
                int i = _f.I(a, b);
                ctx.Surface[i] = _oY + OutRise;
                ctx.TopMaterial[i] = Material.Stone;
            }

        // a dirt path loops round the middle through five waypoints, start and landmark among them
        var way = new List<(int A, int B)> { (_sA, _sB + 4), (36, 66), (34, 126), (_lA, _lB - 14), (N - 34, 126), (N - 36, 66), (_sA, _sB + 4) };
        bool Blocked(int x, int z)
        {
            var (a, b) = Local(x, z);
            return (a >= _oA - 1 && a <= _oA + OutW && b >= _oB - 1 && b <= _oB + OutD)
                   || (Math.Abs(a - _pondA) <= _pondR + 1 && Math.Abs(b - _pondB) <= _pondR + 1)
                   || (a >= _shedA - 2 && a < _shedA + 12 && b >= _shedB - 2 && b < _shedB + 10);
        }
        for (int k = 1; k < way.Count; k++)
        {
            var from = _f.W(way[k - 1].A, way[k - 1].B);
            var to = _f.W(way[k].A, way[k].B);
            foreach (var (x, z) in SceneTools.FindPath(ctx.Surface, from, to, Blocked) ?? new List<(int, int)>())
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int px = x + dx, pz = z + dz;
                        if (px < 0 || pz < 0 || px >= N || pz >= N || ctx.WaterTop[pz * N + px] > 0) continue;
                        ctx.Path[pz * N + px] = true;
                        ctx.TopMaterial[pz * N + px] = Material.Soil;
                    }
        }
    }

    bool InOutcrop(int a, int b) => a >= _oA && a < _oA + OutW && b >= _oB && b < _oB + OutD;

    (int A, int B) Local(int x, int z)
    {
        // invert the frame: try the four mappings' inverse by side
        return _f.Side switch
        {
            0 => (x, z),
            1 => (z, N - 1 - x),
            2 => (N - 1 - x, N - 1 - z),
            _ => (N - 1 - z, x),
        };
    }

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        var shedY = SceneTools.MaxSurface(ctx, _f, _shedA, _shedB, 10, 8);
        SceneTools.Place(ctx, "shed_shell", _f.Origin(_shedA, _shedB, 10, 8, shedY), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
        // a few runs of old fence by the path
        for (int k = 0; k < 3; k++)
        {
            int a = ctx.Rng.NextInt(30, 150), b = ctx.Rng.NextInt(50, 130), len = ctx.Rng.NextInt(8, 20);
            if (InOutcrop(a, b) || Math.Abs(a - _pondA) < _pondR + 4 && Math.Abs(b - _pondB) < _pondR + 4) continue;
            int y = SceneTools.MaxSurface(ctx, _f, a, b, len, 1);
            var fence = SceneTools.Place(ctx, "fence_segment", _f.Origin(a, b, len, 1, y), LocalFrame.Along(_f.FacingOf(1, 0)),
                new PlaceOptions(StretchTo: len, Missing: 0.15));
            ctx.MarkOld(fence);
        }
        Landmark(ctx);
        SceneTools.Scatter(ctx, (x, z, mat, r) =>
        {
            float size = (float)(0.6 + 0.5 * ctx.Noise.Hash01(x, z, 78));
            if (mat == Material.Grass && r < 1.0 / 30)
                return new Prop(PropKind.Plant, x, z, size * 0.7f, ctx.Noise.Hash01(x, z, 79) < 0.5 ? ctx.Colors.Accent : ctx.Colors.Light);
            if (mat == Material.Grass && r is >= 0.5 and < 0.5 + 1.0 / 110) return new Prop(PropKind.Tree, x, z, size + 0.3f, ctx.Colors.Accent);
            if (mat == Material.Grass && r is >= 0.7 and < 0.7 + 1.0 / 120) return new Prop(PropKind.Bush, x, z, size, ctx.Colors.Accent);
            if (r is >= 0.85 and < 0.85 + 1.0 / 250) return new Prop(PropKind.Rock, x, z, size, ctx.ColorOf(Material.Stone));
            return null;
        }, margin: 2);
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        int y = SceneTools.MaxSurface(ctx, _f, _lA - 10, _lB - 10, 21, 21);
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        switch (_landmark)
        {
            case 0:
                SceneTools.Place(ctx, "windmill", _f.Origin(_lA - 10, _lB - 4, 21, 9, y), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
                ctx.Landmark = new Landmark("windmill", Center(_lA, _lB, y + 16));
                break;
            case 1:
            {
                var (x, z) = _f.W(_lA, _lB);
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, 4.5f, ctx.Colors.Accent));
                for (int b = _lB - 2; b <= _lB + 2; b++)
                    for (int a = _lA - 2; a <= _lA + 2; a++)
                        if (Math.Abs(a - _lA) + Math.Abs(b - _lB) <= 3)
                        {
                            var (rx, rz) = _f.W(a, b);
                            int top = SceneTools.TopFree(ctx.World, rx, rz);
                            if (a != _lA || b != _lB) ctx.World.Set(rx, top, rz, Block.Solid(ctx.ColorOf(Material.Wood), Material.Wood));
                        }
                ctx.Structures.Add(_f.Box("lone_tree_roots", _lA - 2, _lB - 2, 5, 5, y, y));
                ctx.Landmark = new Landmark("lone_tree", Center(_lA, _lB, y + 12));
                break;
            }
            default:
                for (int k = 0; k < 9; k++)
                {
                    double t = k * 2 * Math.PI / 9;
                    int a = _lA + (int)Math.Round(Math.Cos(t) * 9), b = _lB + (int)Math.Round(Math.Sin(t) * 9);
                    int top = ctx.Rng.NextInt(10, 15);
                    for (int db = 0; db < 2; db++)
                        for (int da = 0; da < 2; da++)
                        {
                            var (x, z) = _f.W(a + da, b + db);
                            SceneTools.Support(ctx.World, x, z, 1, 1, y, stone);
                            for (int yy = y; yy < y + top; yy++) ctx.World.Set(x, yy, z, stone);
                        }
                }
                var ring = _f.Box("stone_circle", _lA - 9, _lB - 9, 20, 20, y, y + 14);
                ctx.Structures.Add(ring);
                ctx.MarkOld(ring);
                ctx.Landmark = new Landmark("stone_circle", Center(_lA, _lB, y + 6));
                break;
        }
    }

    BlockPos Center(int a, int b, int y)
    {
        var (x, z) = _f.W(a, b);
        return new BlockPos(x, y, z);
    }

    public SceneView View(SceneContext ctx)
    {
        var (sx, sz) = _f.W(_sA, _sB);
        var spawn = new BlockPos(sx, ctx.Surface[sz * N + sx], sz);
        var target = Center(_lA, _lB, SceneTools.MaxSurface(ctx, _f, _lA - 1, _lB - 1, 3, 3) + 12);
        var away = _f.FacingOf(0, 1);
        var candidates = new List<MuralSpot>
        {
            // the outcrop's far face, looking away from the start
            LocalFrame.Spot(_f.W(_oA + (OutW - MuralW) / 2, _oB + OutD - 1), _f.W(_oA + (OutW + MuralW) / 2 - 1, _oB + OutD - 1), _oY, away, MuralH),
        };
        // the shed's back wall (its back faces +b)
        int shedY = SceneTools.MaxSurface(ctx, _f, _shedA, _shedB, 10, 8);
        candidates.Add(LocalFrame.Spot(_f.W(_shedA + 1, _shedB + 7), _f.W(_shedA + 8, _shedB + 7), shedY + 1, away, 7));
        return new SceneView(spawn, target, candidates);
    }
}
