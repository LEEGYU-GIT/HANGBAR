namespace Hangbar.Core.World.Gen;

/// <summary>
/// Beach (v2 plan Task 11). The sea lies on one side chosen by the seed behind a curving shoreline; its floor sinks
/// from the shore to y 44. A sand beach (top blocks y 56–62) rises to dunes and grass. A pier runs out to sea, a
/// lifeguard tower and one or two huts (one you can walk into) stand on the sand, parasols, rocks, trees and a line of
/// floats dress it. A rocky bluff closes one end of the beach and hides a sea cave. The landmark is a shipwreck, a sea
/// arch or a lighthouse on the bluff. The first view looks from the sand straight out to sea.
/// </summary>
/// <remarks>
/// Everything is laid out in local coordinates — a runs along the shore, b runs from the sea (small b) inland — and
/// turned into world columns by a <see cref="LocalFrame"/> for the side the seed picked.
/// </remarks>
public sealed class BeachScene : IScene
{
    const int N = SceneContext.Size, Sea = SceneContext.WaterLevel;
    const int HutW = 10, HutD = 8, PierW = 4, PierLand = 4, PierSea = 36, PierLen = PierLand + PierSea;
    const int BluffW = 16, BluffOut = 6, BluffDeep = 34, BluffTop = 72;
    const int CaveA = 2, CaveW = 12, CaveDeep = 10, CaveFloor = 54, CaveRoof = 66;
    const int OutcropW = 30, OutcropD = 4, OutcropRise = 16, OutcropMuralW = 24, OutcropMuralH = 14;
    const int SpawnInland = 14, TargetOut = 40;

    public Scene Kind => Scene.Beach;

    LocalFrame _f;
    int _e, _beachW, _sA, _pierA, _towerA, _bluffA0, _bluffFront, _outcropA0, _outcropB0, _outcropY, _landmark;
    readonly int[] _shore = new int[N]; // per a: the first land b (b < shore is sea)
    readonly List<(int A, int B, int Y)> _huts = new();

    // ---- the local frame -------------------------------------------------------------------------------------

    (int X, int Z) W(int a, int b) => _f.W(a, b);
    int I(int a, int b) => _f.I(a, b);
    BlockPos Origin(int a0, int b0, int wa, int wb, int y) => _f.Origin(a0, b0, wa, wb, y);
    static Rotation Along(Facing f) => LocalFrame.Along(f);
    static Rotation FrontTo(Facing f) => LocalFrame.FrontTo(f);
    Facing Seaward => _f.FacingOf(0, -1);
    Facing Landward => _f.FacingOf(0, 1);

    bool InBluff(int a, int b) => a >= _bluffA0 && a < _bluffA0 + BluffW && b >= _bluffFront && b < _bluffFront + BluffDeep;
    bool InOutcrop(int a, int b) => a >= _outcropA0 && a < _outcropA0 + OutcropW && b >= _outcropB0 && b < _outcropB0 + OutcropD;

    // ---- shape -----------------------------------------------------------------------------------------------

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        var noise = ctx.Noise;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _e = rng.Next() < 0.5 ? -1 : 1; // the end of the beach the bluff closes
        _beachW = rng.NextInt(26, 33);
        double amp = 4 + 6 * rng.Next(), waves = 1 + rng.Next(), phase = rng.Next() * 2 * Math.PI;
        int baseShore = rng.NextInt(80, 91);
        for (int a = 0; a < N; a++)
            _shore[a] = Math.Clamp((int)Math.Round(baseShore + amp * Math.Sin(2 * Math.PI * waves * a / N + phase)
                                                   + 6 * (noise.Sample(a, 0, 24, 31) - 0.5)), 72, 100);

        _bluffA0 = _e < 0 ? 18 : N - 18 - BluffW;
        _bluffFront = _shore[_bluffA0 + BluffW / 2] - BluffOut;
        int away = -_e;
        _sA = _e < 0 ? rng.NextInt(100, 121) : rng.NextInt(72, 93);
        _pierA = _sA + away * rng.NextInt(20, 27) - (away < 0 ? PierW - 1 : 0);
        _towerA = _sA + _e * rng.NextInt(12, 16);
        _landmark = rng.NextInt(0, 3);

        var h = new int[N * N];
        for (int b = 0; b < N; b++)
            for (int a = 0; a < N; a++)
            {
                int t = b - _shore[a];
                double v;
                if (t < 0)
                {
                    double s = Smooth(Math.Min(1, -t / 55.0));
                    v = Math.Clamp(55 - 11 * s + 1.4 * (noise.Sample(a, b, 9, 41) - 0.5), 44, 55);
                }
                else if (t < _beachW)
                    v = Math.Min(63, 57 + 6.0 * t / _beachW + 0.8 * (noise.Sample(a, b, 7, 42) - 0.5));
                else
                {
                    double u = t - _beachW;
                    v = 63 + 2.5 * Math.Exp(-(u - 6) * (u - 6) / 18) + 7 * Smooth(Math.Min(1, u / 45)) + 6 * (noise.Sample(a, b, 28, 43) - 0.5);
                }
                h[I(a, b)] = (int)Math.Round(v);
            }

        // flat plots: huts on the back of the beach, the outcrop behind the dunes
        var pins = Enumerable.Repeat(Slope.NoPin, N * N).ToArray();
        void Pad(int a0, int b0, int wa, int wb, int y)
        {
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++)
                {
                    pins[I(a, b)] = y;
                    ctx.NoCaves[I(a, b)] = true;
                }
        }
        void Hut(int center)
        {
            int a0 = center - HutW / 2;
            int b0 = Enumerable.Range(a0, HutW).Max(a => _shore[a]) + 20;
            int y = h[I(center, b0 + HutD / 2)];
            Pad(a0 - 2, b0 - 2, HutW + 4, HutD + 4, y);
            _huts.Add((a0, b0, y));
        }
        Hut(_sA + _e * 44);
        if (rng.Next() < 0.6) Hut(_sA + away * 46);

        _outcropA0 = (_e < 0 ? 52 : 140) - OutcropW / 2;
        _outcropB0 = Enumerable.Range(_outcropA0, OutcropW).Max(a => _shore[a]) + _beachW + 14;
        _outcropY = h[I(_outcropA0 + OutcropW / 2, _outcropB0)];
        Pad(_outcropA0 - 2, _outcropB0 - 2, OutcropW + 4, OutcropD + 5, _outcropY);

        var limited = Slope.Limit(h, pins, N, 2);
        Array.Copy(limited, ctx.Surface, limited.Length);

        // the rocky bluff at one end and the outcrop behind the dunes stand up after the slope limit: they are cliffs
        for (int b = 0; b < N; b++)
            for (int a = 0; a < N; a++)
            {
                int i = I(a, b);
                if (InBluff(a, b))
                {
                    ctx.Surface[i] = BluffTop + (int)Math.Round(3 * (noise.Sample(a, b, 6, 44) - 0.5));
                    ctx.NoCaves[i] = true;
                }
                else if (InOutcrop(a, b))
                    ctx.Surface[i] = _outcropY + OutcropRise;
            }

        for (int b = 0; b < N; b++)
            for (int a = 0; a < N; a++)
            {
                int i = I(a, b), t = b - _shore[a];
                if (!InBluff(a, b) && t < 0 && ctx.Surface[i] < Sea) ctx.WaterTop[i] = Sea;
                if (t >= 0 && t <= 16) ctx.NoCaves[i] = true; // no cave mouths on the sand by the water
                ctx.TopMaterial[i] = InBluff(a, b) || InOutcrop(a, b) ? Material.Stone
                    : t < _beachW ? Material.Sand
                    : Steep(ctx, a, b) >= 3 ? Material.Stone
                    : Material.Grass;
            }
        int spawnB = _shore[_sA] + SpawnInland;
        for (int b = spawnB - 2; b <= spawnB + 2; b++)
            for (int a = _sA - 2; a <= _sA + 2; a++) ctx.NoCaves[I(a, b)] = true;
    }

    static double Smooth(double t) => t * t * (3 - 2 * t);

    int Steep(SceneContext ctx, int a, int b)
    {
        int s = 0, here = ctx.Surface[I(a, b)];
        foreach (var (da, db) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int na = a + da, nb = b + db;
            if (na >= 0 && nb >= 0 && na < N && nb < N) s = Math.Max(s, Math.Abs(ctx.Surface[I(na, nb)] - here));
        }
        return s;
    }

    // ---- build -----------------------------------------------------------------------------------------------

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        Pier(ctx);

        int tb = _shore[_towerA] + 6, ta0 = _towerA - 2;
        int ty = MinSurface(ctx, ta0, tb, 5, 5);
        ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("lifeguard_tower"), Origin(ta0, tb, 5, 5, ty), FrontTo(Seaward),
            ctx.Colors, new PlaceOptions(), ctx.Rng));

        foreach (var (a0, b0, y) in _huts)
            ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("beach_hut"), Origin(a0, b0, HutW, HutD, y), FrontTo(Seaward),
                ctx.Colors, new PlaceOptions(), ctx.Rng)); // door to the sea, back wall inland

        // the sea cave: a flooded room cut into the bluff's sea face
        for (int b = _bluffFront; b < _bluffFront + CaveDeep; b++)
            for (int a = _bluffA0 + CaveA; a < _bluffA0 + CaveA + CaveW; a++)
            {
                var (x, z) = W(a, b);
                for (int y = CaveFloor; y < CaveRoof; y++) w.Set(x, y, z, y < Sea ? Block.Water : Block.Air);
            }

        Landmark(ctx);
        Props(ctx);
    }

    void Pier(SceneContext ctx)
    {
        var w = ctx.World;
        int bLand = _shore[_pierA] + PierLand, b0 = bLand - PierLen;
        int rootY = Enumerable.Range(0, PierW).Max(k => ctx.Surface[I(_pierA + k, bLand - 1)]);
        int deckY = Math.Max(Sea, rootY - 1);
        var post = PartLibrary.Get("pier_post");
        for (int s = 0; s < PierLen; s += 4)
            foreach (int k in new[] { 0, PierW - 1 })
            {
                int a = _pierA + k, b = bLand - 1 - s, i = I(a, b);
                if (ctx.WaterTop[i] == 0) continue; // the land end needs no posts
                var (x, z) = W(a, b);
                int floor = ctx.Surface[i], start = deckY - 4 * ((deckY - floor + 3) / 4);
                for (int y = start; y < deckY; y += 4)
                    PartPlacer.Place(w, post, new BlockPos(x, y, z), Rotation.R0, ctx.Colors, new PlaceOptions(), ctx.Rng);
            }
        ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("pier_deck"), Origin(_pierA, b0, PierW, PierLen, deckY), Along(Seaward),
            ctx.Colors, new PlaceOptions(StretchTo: PierLen), ctx.Rng));
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        int away = -_e;
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        switch (_landmark)
        {
            case 0:
            {
                int ca = _sA + away * 38, a0 = ca - 3, b0 = _shore[ca] - 26 - 16;
                int y = MinSurface(ctx, a0, b0, 6, 16);
                var wreck = PartPlacer.Place(w, PartLibrary.Get("shipwreck"), Origin(a0, b0, 6, 16, y), FrontTo(Seaward), ctx.Colors,
                    new PlaceOptions(Missing: 0.15), ctx.Rng);
                ctx.Structures.Add(wreck);
                ctx.MarkOld(wreck);
                var (cx, cz) = W(ca, b0 + 8);
                ctx.Landmark = new Landmark("shipwreck", new BlockPos(cx, y + 3, cz));
                break;
            }
            case 1:
            {
                int ca = _sA + away * 40, cb = _shore[ca] - 26, a0 = ca - 6, b0 = cb - 1;
                int floor = MinSurface(ctx, a0, b0, 13, 3);
                for (int b = b0; b < b0 + 3; b++)
                    for (int a = a0; a < a0 + 13; a++)
                    {
                        var (x, z) = W(a, b);
                        bool pillar = a < a0 + 3 || a >= a0 + 10;
                        for (int y = floor - 2; y <= 70; y++)
                            if (pillar || y >= 65) w.Set(x, y, z, stone);
                    }
                ctx.Structures.Add(_f.Box("sea_arch", a0, b0, 13, 3, floor - 2, 70));
                var (cx, cz) = W(ca, cb);
                ctx.Landmark = new Landmark("sea_arch", new BlockPos(cx, 66, cz));
                break;
            }
            default:
            {
                int a0 = _bluffA0 + (BluffW - 7) / 2, b0 = _bluffFront + 14;
                int y = MaxSurface(ctx, a0, b0, 7, 7);
                for (int b = b0; b < b0 + 7; b++)
                    for (int a = a0; a < a0 + 7; a++)
                    {
                        var (x, z) = W(a, b);
                        for (int yy = y - 1; yy > 1 && !Block.IsSolid(w.Get(x, yy, z)); yy--) w.Set(x, yy, z, stone);
                    }
                var tower = PartPlacer.Place(w, PartLibrary.Get("lighthouse"), Origin(a0, b0, 7, 7, y), FrontTo(Seaward), ctx.Colors,
                    new PlaceOptions(), ctx.Rng);
                ctx.Structures.Add(tower);
                var (cx, cz) = W(a0 + 3, b0 + 3);
                ctx.Landmark = new Landmark("lighthouse", new BlockPos(cx, y + 15, cz));
                break;
            }
        }
    }

    int MinSurface(SceneContext ctx, int a0, int b0, int wa, int wb) =>
        Enumerable.Range(0, wa * wb).Min(k => ctx.Surface[I(a0 + k % wa, b0 + k / wa)]);

    int MaxSurface(SceneContext ctx, int a0, int b0, int wa, int wb) =>
        Enumerable.Range(0, wa * wb).Max(k => ctx.Surface[I(a0 + k % wa, b0 + k / wa)]);

    void Props(SceneContext ctx)
    {
        var w = ctx.World;
        int spawnB = _shore[_sA] + SpawnInland;
        bool Covered(int x, int z) => ctx.Structures.Any(s => x >= s.Min.X - 1 && x <= s.Max.X + 1 && z >= s.Min.Z - 1 && z <= s.Max.Z + 1);
        for (int b = 2; b < N - 2; b++)
            for (int a = 2; a < N - 2; a++)
            {
                int t = b - _shore[a];
                if (t < 0 || (Math.Abs(a - _sA) <= 3 && Math.Abs(b - spawnB) <= 3)) continue;
                var (x, z) = W(a, b);
                if (Covered(x, z)) continue;
                int top = Top(w, x, z);
                if (top < 0 || !Block.IsSolid(w.Get(x, top, z)) || Block.IsWater(w.Get(x, top + 1, z))) continue;
                var mat = Block.MaterialOf(w.Get(x, top, z));
                double r = ctx.Noise.Hash01(x, z, 77), size = 0.7 + 0.5 * ctx.Noise.Hash01(x, z, 78);
                if (mat == Material.Sand && t >= 3 && t < _beachW - 3 && r < 1.0 / 110)
                    ctx.Props.Add(new Prop(PropKind.Parasol, x, z, (float)size, ctx.Colors.Accent));
                else if (mat == Material.Grass && r < 1.0 / 70)
                    ctx.Props.Add(new Prop(PropKind.Tree, x, z, (float)(size + 0.2), ctx.Colors.Accent));
                else if ((mat == Material.Stone && r < 1.0 / 150) || (mat == Material.Sand && r > 1 - 1.0 / 500))
                    ctx.Props.Add(new Prop(PropKind.Rock, x, z, (float)size, ctx.ColorOf(Material.Stone)));
            }
        // a line of floats marking the swimming area in front of the spawn
        for (int a = _sA - 24; a <= _sA + 24; a += 3)
        {
            var (x, z) = W(a, _shore[a] - 18);
            if (!Block.IsWater(w.Get(x, Sea - 1, z)) || w.Get(x, Sea, z) != Block.Air) continue;
            ctx.Props.Add(new Prop(PropKind.Float, x, z, 0.6f, 0xE84A2F, Sea));
        }
    }

    static int Top(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y;
        return -1;
    }

    // ---- view ------------------------------------------------------------------------------------------------

    public SceneView View(SceneContext ctx)
    {
        var (sx, sz) = W(_sA, _shore[_sA] + SpawnInland);
        var spawn = new BlockPos(sx, ctx.Surface[sz * N + sx], sz);
        var (tx, tz) = W(_sA, _shore[_sA] - TargetOut);
        var target = new BlockPos(tx, Sea, tz);

        var candidates = new List<MuralSpot>
        {
            // the back wall of the sea cave, facing out to sea
            Spot(_bluffA0 + CaveA + 1, _bluffA0 + CaveA + CaveW - 2, _bluffFront + CaveDeep, CaveFloor, Seaward, 10),
            // the rock face behind the dunes, facing inland
            Spot(_outcropA0 + (OutcropW - OutcropMuralW) / 2, _outcropA0 + (OutcropW + OutcropMuralW) / 2 - 1,
                _outcropB0 + OutcropD - 1, _outcropY, Landward, OutcropMuralH),
        };
        // the back walls of the huts, facing inland
        foreach (var (a0, b0, y) in _huts) candidates.Add(Spot(a0 + 1, a0 + HutW - 2, b0 + HutD - 1, y + 1, Landward, 10));
        return new SceneView(spawn, target, candidates);
    }

    /// <summary>A mural on the wall row b from column a1 to a2.</summary>
    MuralSpot Spot(int a1, int a2, int b, int y, Facing f, int height) => LocalFrame.Spot(W(a1, b), W(a2, b), y, f, height);
}
