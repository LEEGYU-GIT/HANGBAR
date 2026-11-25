namespace Hangbar.Core.World.Gen;

/// <summary>
/// Pool (v2 plan Task 12). Flat ground with a tiled deck (top blocks y 56) inside a fence. The pool is 64–80 long,
/// 32–48 wide in lanes of 8, tiled light with dark lane lines, 5 deep at the shallow end and 8 at the deep end, its
/// water level with the deck. Steps of 2 blocks lead out in a shallow corner; ladders stand at the lane ends. A diving
/// board and a lifeguard chair stand at the deep end, a changing room beyond it holds a walk-in plant room whose door
/// faces away from the start; the photo hangs framed inside. Floats mark the lanes. The landmark is a water slide or a
/// high-dive tower. The first view looks from the middle of the shallow end down the pool.
/// </summary>
/// <remarks>Local frame: a runs across the pool, b along it from the shallow end (small b) to the deep end.</remarks>
public sealed class PoolScene : IScene
{
    const int N = SceneContext.Size, Edge = SceneContext.EdgeBand;
    const int DeckTop = 56, Ground = DeckTop + 1, Level = DeckTop + 1; // Surface of the deck, WaterTop of the pool
    const int Shallow = 5, Deep = 8, Lane = 8;
    const int EndDeck = 14, SideDeck = 18, RoomW = 18, RoomD = 11, Behind = 7, Gate = 6, Walkway = 3;
    const int SpawnBack = 7;

    public Scene Kind => Scene.Pool;

    LocalFrame _f;
    int _a0, _b0, _len, _wid, _lanes, _landmark, _slideSide;
    int _deckA0, _deckA1, _deckB0, _deckB1; // deck box, exclusive ends
    MuralSpot? _mural;

    Facing Toward(int da, int db) => _f.FacingOf(da, db);
    bool InPool(int a, int b) => a >= _a0 && a < _a0 + _wid && b >= _b0 && b < _b0 + _len;
    bool OnDeck(int a, int b) => a >= _deckA0 && a < _deckA1 && b >= _deckB0 && b < _deckB1;
    int Center => _a0 + _wid / 2;

    // ---- shape -----------------------------------------------------------------------------------------------

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _len = rng.NextInt(64, 81);
        _lanes = rng.NextInt(4, 7);
        _wid = _lanes * Lane;
        int after = _len + EndDeck + RoomD + Behind;
        _b0 = rng.NextInt(Edge + 2 + EndDeck, N - Edge - 2 - after + 1);
        _a0 = rng.NextInt(Edge + 2 + SideDeck, N - Edge - 2 - SideDeck - _wid + 1);
        _landmark = rng.NextInt(0, 2);
        _slideSide = rng.Next() < 0.5 ? -1 : 1;
        _deckA0 = _a0 - SideDeck;
        _deckA1 = _a0 + _wid + SideDeck;
        _deckB0 = _b0 - EndDeck;
        _deckB1 = _b0 + after;

        for (int b = 0; b < N; b++)
            for (int a = 0; a < N; a++)
            {
                int i = _f.I(a, b);
                ctx.Surface[i] = Ground;
                bool deck = OnDeck(a, b);
                ctx.TopMaterial[i] = deck || InPool(a, b) ? Material.Tile : Material.Grass;
                if (a >= _deckA0 - 2 && a < _deckA1 + 2 && b >= _deckB0 - 2 && b < _deckB1 + 2) ctx.NoCaves[i] = true;
                if (InPool(a, b))
                {
                    double t = Math.Clamp((b - _b0 - 0.4 * _len) / (0.3 * _len), 0, 1);
                    ctx.Surface[i] = Level - (int)Math.Round(Shallow + (Deep - Shallow) * t);
                    ctx.WaterTop[i] = Level;
                    ctx.Stairs[i] = true; // the sloping floor is a ramp: walked up without jumping (walls stay 5+ high)
                }
                else if (deck && a >= _a0 - Walkway && a < _a0 + _wid + Walkway && b >= _b0 - Walkway && b < _b0 + _len + Walkway)
                    ctx.Path[i] = true; // the walkway round the pool: worn, not mossy
            }

        // the way out: steps of 2 blocks in the shallow corner of lane 0 (floor 52 → 54 → 56 → deck 57); they and the
        // deck edge above them are stairs (no jump)
        for (int b = _b0; b < _b0 + 4; b++)
            for (int a = _a0; a < _a0 + 4; a++)
                ctx.Surface[_f.I(a, b)] = b < _b0 + 2 ? Level - 1 : Level - 3;
        for (int b = _b0 - 1; b < _b0 + 5; b++)
            for (int a = _a0; a < _a0 + 4; a++) ctx.Stairs[_f.I(a, b)] = true;
    }

    // ---- build -----------------------------------------------------------------------------------------------

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        Line(ctx);

        // the deep end: diving board in lane 1, a lifeguard chair on the side deck away from the slide
        int board = _a0 + Lane + Lane / 2;
        Add(ctx, "diving_board", _f.Origin(board - 1, _b0 + _len - 6, 3, 9, Ground), LocalFrame.FrontTo(Toward(0, -1)));
        int chairSide = -_slideSide;
        int chairA = chairSide < 0 ? _a0 - 6 : _a0 + _wid + 3;
        Add(ctx, "lifeguard_chair", _f.Origin(chairA, _b0 + _len - 16, 3, 3, Ground), LocalFrame.FrontTo(Toward(-chairSide, 0)));

        // the changing room beyond the deep end, front to the pool, plant-room door away from the start
        var room = Add(ctx, "changing_room", _f.Origin(Center - RoomW / 2, _b0 + _len + EndDeck, RoomW, RoomD, Ground),
            LocalFrame.FrontTo(Toward(0, -1)));
        var ma = room.Anchors["mural_a"];
        var mb = room.Anchors["mural_b"];
        _mural = LocalFrame.Spot((ma.X, ma.Z), (mb.X, mb.Z), ma.Y, Toward(0, 1), 8, framed: true);

        Landmark(ctx);
        Fence(ctx);
        Ladders(ctx);
        Props(ctx);
    }

    PlacedPart Add(SceneContext ctx, string part, BlockPos origin, Rotation r, PlaceOptions? options = null)
    {
        var placed = PartPlacer.Place(ctx.World, PartLibrary.Get(part), origin, r, ctx.Colors, options ?? new PlaceOptions(), ctx.Rng);
        ctx.Structures.Add(placed);
        return placed;
    }

    /// <summary>Tiles the basin: every solid block under or beside the pool water turns light tile, lane lines dark.</summary>
    void Line(SceneContext ctx)
    {
        var w = ctx.World;
        uint light = Block.Solid(ctx.Colors.Light, Material.Tile), dark = Block.Solid(ctx.Colors.Dark, Material.Tile);
        for (int b = _b0; b < _b0 + _len; b++)
            for (int a = _a0; a < _a0 + _wid; a++)
            {
                var (x, z) = _f.W(a, b);
                int floor = ctx.Surface[_f.I(a, b)];
                for (int y = floor; y < Level; y++)
                    foreach (var (dx, dy, dz) in new[] { (0, -1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1) })
                        if (Block.IsSolid(w.Get(x + dx, y + dy, z + dz))) w.Set(x + dx, y + dy, z + dz, light);
            }
        for (int lane = 0; lane < _lanes; lane++)
            for (int b = _b0 + 2; b < _b0 + _len - 2; b++)
            {
                int a = _a0 + lane * Lane + Lane / 2;
                var (x, z) = _f.W(a, b);
                w.Set(x, ctx.Surface[_f.I(a, b)] - 1, z, dark);
            }
    }

    void Landmark(SceneContext ctx)
    {
        if (_landmark == 0)
        {
            // the slide's tower stands on the side deck; its chute ends over lane 0 or the last lane
            int bs = _b0 + _len / 2 - 2;
            int a0 = _slideSide < 0 ? _a0 - 14 : _a0 + _wid - 2;
            var slide = Add(ctx, "water_slide", _f.Origin(a0, bs, 16, 4, Ground), LocalFrame.FrontTo(Toward(_slideSide, 0)));
            int towerA = _slideSide < 0 ? a0 + 1 : a0 + 14;
            var (cx, cz) = _f.W(towerA, bs + 1);
            ctx.Landmark = new Landmark("water_slide", new BlockPos(cx, Ground + 13, cz));
        }
        else
        {
            int ca = _a0 + Lane * (_lanes - 2) + Lane / 2;
            Add(ctx, "high_dive_tower", _f.Origin(ca - 2, _b0 + _len - 5, 5, 10, Ground), LocalFrame.FrontTo(Toward(0, -1)));
            var (cx, cz) = _f.W(ca, _b0 + _len + 2);
            ctx.Landmark = new Landmark("high_dive_tower", new BlockPos(cx, Ground + 20, cz));
        }
    }

    /// <summary>A fence round the deck with a gate in the middle of each side.</summary>
    void Fence(SceneContext ctx)
    {
        int a0 = _deckA0 - 1, a1 = _deckA1, b0 = _deckB0 - 1, b1 = _deckB1;
        void Run(int from, int to, bool alongA, int fixedAt)
        {
            int len = to - from;
            if (len < 2) return;
            var origin = alongA ? _f.Origin(from, fixedAt, len, 1, Ground) : _f.Origin(fixedAt, from, 1, len, Ground);
            Add(ctx, "fence_segment", origin, LocalFrame.Along(alongA ? Toward(1, 0) : Toward(0, 1)), new PlaceOptions(StretchTo: len));
        }
        int ga = (a0 + a1) / 2 - Gate / 2, gb = (b0 + b1) / 2 - Gate / 2;
        foreach (int b in new[] { b0, b1 })
        {
            Run(a0, ga, true, b);
            Run(ga + Gate, a1 + 1, true, b);
        }
        foreach (int a in new[] { a0, a1 })
        {
            Run(b0 + 1, gb, false, a);
            Run(gb + Gate, b1, false, a);
        }
    }

    /// <summary>A ladder at each lane end, where the steps or another structure are not already.</summary>
    void Ladders(SceneContext ctx)
    {
        for (int lane = 0; lane < _lanes; lane++)
        {
            int la = _a0 + lane * Lane + 1;
            foreach (bool shallow in new[] { true, false })
            {
                if (shallow && la < _a0 + 4) continue; // the steps are here
                int bm = shallow ? _b0 - 1 : _b0 + _len - 1;
                var box = _f.Box("pool_ladder", la, bm, 3, 2, Level - Shallow, Level + 2);
                if (ctx.Structures.Any(s => Overlaps(s, box, 1))) continue;
                Add(ctx, "pool_ladder", _f.Origin(la, bm, 3, 2, Level - Shallow), LocalFrame.FrontTo(Toward(0, shallow ? 1 : -1)));
            }
        }
    }

    static bool Overlaps(PlacedPart s, PlacedPart box, int margin) =>
        box.Min.X <= s.Max.X + margin && box.Max.X >= s.Min.X - margin && box.Min.Z <= s.Max.Z + margin && box.Max.Z >= s.Min.Z - margin;

    void Props(SceneContext ctx)
    {
        var w = ctx.World;
        // floats on every lane boundary, every 2 blocks, on the water
        for (int lane = 1; lane < _lanes; lane++)
            for (int b = _b0 + 1; b < _b0 + _len - 1; b += 2)
            {
                var (x, z) = _f.W(_a0 + lane * Lane, b);
                if (!Block.IsWater(w.Get(x, Level - 1, z)) || w.Get(x, Level, z) != Block.Air) continue;
                ctx.Props.Add(new Prop(PropKind.Float, x, z, 0.4f, (b / 2) % 2 == 0 ? ctx.Colors.Light : ctx.Colors.Accent, Level));
            }

        int spawnB = _b0 - SpawnBack;
        bool Covered(int x, int z) => ctx.Structures.Any(s => x >= s.Min.X - 1 && x <= s.Max.X + 1 && z >= s.Min.Z - 1 && z <= s.Max.Z + 1);
        for (int b = 2; b < N - 2; b++)
            for (int a = 2; a < N - 2; a++)
            {
                if (InPool(a, b) || ctx.Path[_f.I(a, b)] || (Math.Abs(a - Center) <= 3 && Math.Abs(b - spawnB) <= 3)) continue;
                var (x, z) = _f.W(a, b);
                if (Covered(x, z) || !Block.IsSolid(w.Get(x, Ground - 1, z)) || w.Get(x, Ground, z) != Block.Air) continue;
                double r = ctx.Noise.Hash01(x, z, 77), size = 0.7 + 0.5 * ctx.Noise.Hash01(x, z, 78);
                if (OnDeck(a, b))
                {
                    bool rim = a == _deckA0 || a == _deckA1 - 1 || b == _deckB0 || b == _deckB1 - 1;
                    if (rim && (a + b) % 6 == 0) ctx.Props.Add(new Prop(PropKind.Plant, x, z, (float)size, ctx.Colors.Accent));
                    else if (!rim && r < 1.0 / 60) ctx.Props.Add(new Prop(PropKind.Bench, x, z, 1f, ctx.ColorOf(Material.Wood)));
                    else if (!rim && r > 1 - 1.0 / 70) ctx.Props.Add(new Prop(PropKind.Parasol, x, z, (float)size, ctx.Colors.Accent));
                }
                else if (r < 1.0 / 120)
                    ctx.Props.Add(new Prop(PropKind.Tree, x, z, (float)(size + 0.2), ctx.Colors.Accent));
            }
    }

    // ---- view ------------------------------------------------------------------------------------------------

    public SceneView View(SceneContext ctx)
    {
        var (sx, sz) = _f.W(Center, _b0 - SpawnBack);
        var (tx, tz) = _f.W(Center, _b0 + _len - 1);
        var candidates = _mural is { } m ? new List<MuralSpot> { m } : new List<MuralSpot>();
        return new SceneView(new BlockPos(sx, Ground, sz), new BlockPos(tx, Level, tz), candidates);
    }
}
