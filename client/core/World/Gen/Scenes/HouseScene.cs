namespace Hangbar.Core.World.Gen;

/// <summary>
/// House (v2 plan Task 13): a real-scale house in a fenced yard. 56–64 × 40–48 blocks, a basement, two floors and a
/// rooftop; every storey is a 1-block slab and an 11-block room height. Walls are brick, concrete or wood by seed.
/// One of two floor plans, mirrored or not: downstairs a living room, a stairwell, a hall with the front door, a
/// kitchen-dining room and a bathroom; upstairs a bedroom over the living room, a hall, a second bedroom or a study
/// and a bathroom; in the basement a storeroom and the hidden room where the photo hangs framed. Doors are 4 × 9,
/// windows glass 4–8 blocks above the floor, stairs straight with steps of 1. The landmark in the back yard is a
/// treehouse or a greenhouse; one or two neighbours' houses stand outside the fence. The first view looks from the
/// living room at its front window.
/// </summary>
/// <remarks>
/// House coordinates: p runs along the house (0 and L−1 are the end walls), q across it (0 the back wall, D−1 the
/// front wall). They map to the scene's local (a, b) — mirrored in a when the seed says so — and from there to the world.
/// </remarks>
public sealed class HouseScene : IScene
{
    const int N = SceneContext.Size, Ground = 57, Yard = 14, Roof = 80, Parapet = 83;
    const int SpawnP = 8, SpawnQ = 12, DoorW = 4, DoorH = 9;
    static readonly int[] Level = { 45, 57, 69 }; // standing height of the basement, ground floor and upstairs

    public Scene Kind => Scene.House;

    sealed record Room(string Kind, int Floor, int P0, int P1, int Q0, int Q1, Material FloorMat);
    sealed record Door(int Floor, bool OnP, int Wall, int From);

    LocalFrame _f;
    int _l, _d, _xL, _yK, _hq, _hA0, _hB0, _template, _landmark;
    bool _mirror, _study;
    Material _wallMat;
    readonly List<Room> _rooms = new();
    readonly List<Door> _doors = new();

    static int S(int floor) => Level[floor + 1];
    int R0 => _xL + 10; // first column of the rooms beyond the hall

    // ---- house coordinates → world -----------------------------------------------------------------------------

    int A(int p) => _hA0 + (_mirror ? _l - 1 - p : p);
    int B(int q) => _hB0 + q;
    (int X, int Z) Wp(int p, int q) => _f.W(A(p), B(q));
    Facing HF(int dp, int dq) => _f.FacingOf(_mirror ? -dp : dp, dq);
    int AMin(int p0, int wp) => _mirror ? A(p0 + wp - 1) : A(p0);
    BlockPos Origin(int p0, int q0, int wp, int wq, int y) => _f.Origin(AMin(p0, wp), B(q0), wp, wq, y);
    PlacedPart Box(string name, int p0, int q0, int wp, int wq, int y0, int y1) => _f.Box(name, AMin(p0, wp), B(q0), wp, wq, y0, y1);

    void Set(VoxelWorld w, int p, int y, int q, uint b)
    {
        var (x, z) = Wp(p, q);
        w.Set(x, y, z, b);
    }

    void Fill(VoxelWorld w, int p, int q, int y0, int y1, uint b)
    {
        for (int y = y0; y <= y1; y++) Set(w, p, y, q, b);
    }

    // ---- shape ---------------------------------------------------------------------------------------------------

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _l = rng.NextInt(56, 65);
        _d = rng.NextInt(40, 49);
        _xL = rng.NextInt(22, 26);
        _yK = _d - 13;
        _hq = _d / 2 - 6;
        _template = rng.NextInt(0, 2);
        _mirror = rng.Next() < 0.5;
        _study = rng.Next() < 0.4;
        _wallMat = new[] { Material.Brick, Material.Concrete, Material.Wood }[rng.NextInt(0, 3)];
        _landmark = rng.NextInt(0, 2);
        _hA0 = (N - _l) / 2 + rng.NextInt(-6, 7);
        _hB0 = (N - _d) / 2 + rng.NextInt(-6, 7);
        Plan();

        for (int i = 0; i < N * N; i++)
        {
            ctx.Surface[i] = Ground;
            ctx.TopMaterial[i] = Material.Grass;
        }
        for (int q = -3; q < _d + 3; q++)
            for (int p = -3; p < _l + 3; p++) ctx.NoCaves[_f.I(A(p), B(q))] = true;
        // the path from the front door to the gate and a little beyond
        for (int q = _d; q < _d + Yard + 4; q++)
            for (int p = _xL + 3; p < _xL + 3 + DoorW; p++)
            {
                int i = _f.I(A(p), B(q));
                ctx.Path[i] = true;
                ctx.TopMaterial[i] = Material.Concrete;
            }
    }

    /// <summary>The rooms and doors of the chosen floor plan, in house coordinates (rooms are interiors; walls lie between).</summary>
    void Plan()
    {
        int xL = _xL, l = _l, d = _d, yK = _yK;
        void Add(string kind, int floor, int p0, int p1, int q0, int q1, Material m) => _rooms.Add(new Room(kind, floor, p0, p1, q0, q1, m));
        void Doorway(int floor, bool onP, int wall, int from) => _doors.Add(new Door(floor, onP, wall, from));

        foreach (int floor in new[] { -1, 0, 1 }) Add("stair", floor, xL + 1, xL + 9, 1, 21, Material.Concrete);
        Add("living", 0, 1, xL, 1, d - 1, Material.Wood);
        Add("hall", 0, xL + 1, xL + 9, 22, d - 1, Material.Wood);
        Add("bedroom", 1, 1, xL, 1, d - 1, Material.Wood);
        Add("hall", 1, xL + 1, xL + 9, 22, d - 1, Material.Wood);
        string second = _study ? "study" : "bedroom";
        if (_template == 0)
        {
            Add("kitchen", 0, R0, l - 1, 1, yK, Material.Tile);
            Add("bath", 0, R0, l - 1, yK + 1, d - 1, Material.Tile);
            Add(second, 1, R0, l - 1, 1, yK, Material.Wood);
            Add("bath", 1, R0, l - 1, yK + 1, d - 1, Material.Tile);
        }
        else
        {
            Add("kitchen", 0, R0, l - 1, 12, d - 1, Material.Tile);
            Add("bath", 0, R0, l - 1, 1, 11, Material.Tile);
            Add(second, 1, R0, l - 1, 12, d - 1, Material.Wood);
            Add("bath", 1, R0, l - 1, 1, 11, Material.Tile);
        }
        Add("storage", -1, R0, l - 13, 1, d - 1, Material.Concrete);
        Add("hidden", -1, l - 12, l - 1, _hq, _hq + 12, Material.Concrete);

        Doorway(0, true, xL, 1);              // living room → stairwell
        Doorway(0, true, xL, 24);             // living room → hall
        Doorway(0, false, d - 1, xL + 3);     // front door
        Doorway(1, false, 21, xL + 1);        // stair landing → upstairs hall
        Doorway(1, true, xL, 24);             // upstairs hall → bedroom
        Doorway(-1, true, xL + 9, 17);        // stairwell → storeroom
        Doorway(-1, true, l - 13, _hq + 4);   // storeroom → hidden room
        foreach (int floor in new[] { 0, 1 })
            if (_template == 0)
            {
                Doorway(floor, true, xL + 9, 23);
                Doorway(floor, true, xL + 9, yK + 2);
            }
            else
            {
                Doorway(floor, true, xL + 9, 24);
                Doorway(floor, false, 11, R0 + 2);
            }
    }

    // ---- build ---------------------------------------------------------------------------------------------------

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        uint outer = Block.Solid(ctx.ColorOf(_wallMat), _wallMat);
        uint plaster = Block.Solid(ctx.Colors.Light, Material.Concrete);
        uint slab = Block.Solid(ctx.ColorOf(Material.Wood), Material.Wood);
        uint concrete = Block.Solid(ctx.ColorOf(Material.Concrete), Material.Concrete);

        // the solid house: concrete basement block, slabs, outer and inner walls, a parapet round the roof
        for (int q = 0; q < _d; q++)
            for (int p = 0; p < _l; p++)
            {
                bool edge = p == 0 || q == 0 || p == _l - 1 || q == _d - 1;
                for (int y = 44; y <= Parapet; y++)
                {
                    uint b = y <= 55 || y == Roof ? concrete
                        : y == 56 || y == 68 ? slab
                        : y < Roof ? (edge ? outer : plaster)
                        : edge ? outer : Block.Air;
                    Set(w, p, y, q, b);
                }
            }

        foreach (var r in _rooms)
        {
            int s = S(r.Floor);
            uint floorBlock = Block.Solid(ctx.ColorOf(r.FloorMat), r.FloorMat);
            for (int q = r.Q0; q < r.Q1; q++)
                for (int p = r.P0; p < r.P1; p++)
                {
                    Set(w, p, s - 1, q, floorBlock);
                    Fill(w, p, q, s, s + 10, Block.Air);
                }
            ctx.Structures.Add(Box("room_" + r.Kind, r.P0, r.Q0, r.P1 - r.P0, r.Q1 - r.Q0, s, s + 10));
        }

        foreach (var door in _doors)
        {
            int s = S(door.Floor);
            for (int k = 0; k < DoorW; k++)
            {
                var (p, q) = door.OnP ? (door.Wall, door.From + k) : (door.From + k, door.Wall);
                Fill(w, p, q, s, s + DoorH - 1, Block.Air);
            }
            ctx.Structures.Add(door.OnP
                ? Box("doorway", door.Wall, door.From, 1, DoorW, s, s + DoorH - 1)
                : Box("doorway", door.From, door.Wall, DoorW, 1, s, s + DoorH - 1));
        }

        Stairs(w, Block.Solid(ctx.ColorOf(Material.Wood), Material.Wood), plaster, concrete);
        Windows(w, Block.Solid(0xC8E6F0, Material.Glass));
        ctx.Structures.Add(Box("house", 0, 0, _l, _d, 44, Parapet));

        foreach (var r in _rooms) Furnish(ctx, r);
        Yard_(ctx);
    }

    /// <summary>
    /// The stairwell (8 × 20, u = q − 1 from the living-room end): lane U (first 4 columns) climbs ground → upstairs
    /// along +u, lane D climbs upstairs → roof along −u and, below, descends ground → basement along +u. Pockets
    /// no stair reaches are filled in.
    /// </summary>
    void Stairs(VoxelWorld w, uint step, uint plaster, uint concrete)
    {
        int laneU = _xL + 1, laneD = _xL + 5;
        for (int p = laneU; p < laneU + 4; p++)
        {
            for (int k = 1; k <= 12; k++)
            {
                int q = 3 + k + 1;
                Fill(w, p, q, 57, 56 + k, step);
                if (k <= 11) Set(w, p, 68, q, Block.Air); // headroom through the upstairs floor
            }
            for (int u = 16; u <= 19; u++) Fill(w, p, u + 1, 57, 67, plaster);
            for (int u = 0; u <= 3; u++) Fill(w, p, u + 1, 69, 79, plaster);
        }
        for (int p = laneD; p < laneD + 4; p++)
        {
            for (int k = 1; k <= 12; k++)
            {
                int q = 3 + k + 1; // down to the basement
                Fill(w, p, q, 45, 56 - k, concrete);
                Fill(w, p, q, 57 - k, 56, Block.Air);
            }
            for (int u = 16; u <= 19; u++) Fill(w, p, u + 1, 57, 67, plaster);
            for (int u = 0; u <= 6; u++) Fill(w, p, u + 1, 69, 79, plaster);
            for (int k = 1; k <= 12; k++)
            {
                int q = 19 - k + 1; // up to the roof
                Fill(w, p, q, 69, 68 + k, step);
                if (k <= 11) Set(w, p, Roof, q, Block.Air);
            }
        }
    }

    /// <summary>Glass in the outer walls of the rooms (not halls or stairs), rows 4–7 above the floor.</summary>
    void Windows(VoxelWorld w, uint glass)
    {
        foreach (var r in _rooms.Where(r => r.Floor >= 0 && r.Kind is not ("stair" or "hall")))
        {
            int s = S(r.Floor);
            var sides = new List<(bool OnP, int Wall, int From, int To)>();
            if (r.P0 == 1) sides.Add((true, 0, r.Q0, r.Q1));
            if (r.P1 == _l - 1) sides.Add((true, _l - 1, r.Q0, r.Q1));
            if (r.Q0 == 1) sides.Add((false, 0, r.P0, r.P1));
            if (r.Q1 == _d - 1) sides.Add((false, _d - 1, r.P0, r.P1));
            foreach (var (onP, wall, from, to) in sides)
            {
                int len = to - from, width = Math.Min(8, len - 4), c0 = from + (len - width) / 2;
                if (r.Kind == "living" && !onP && wall == _d - 1) (width, c0) = (7, SpawnP - 3); // the window the start looks at
                if (width < 3) continue;
                for (int c = c0; c < c0 + width; c++)
                    for (int y = s + 4; y <= s + 7; y++)
                        if (onP) Set(w, wall, y, c, glass); else Set(w, c, y, wall, glass);
            }
        }
    }

    // ---- furniture -----------------------------------------------------------------------------------------------

    PlacedPart Furn(SceneContext ctx, string name, int p0, int q0, int dp, int dq, int floor, int? stretch = null)
    {
        var part = PartLibrary.Get(name);
        int width = stretch ?? part.Width;
        bool alongP = dp != 0; // the part's depth runs along p
        int wp = alongP ? part.Depth : width, wq = alongP ? width : part.Depth;
        var placed = PartPlacer.Place(ctx.World, part, Origin(p0, q0, wp, wq, S(floor)), LocalFrame.FrontTo(HF(dp, dq)), ctx.Colors,
            new PlaceOptions(StretchTo: stretch), ctx.Rng);
        ctx.Structures.Add(placed);
        return placed;
    }

    void Thing(SceneContext ctx, PropKind kind, int p, int q, int y, float size = 0.5f, uint? color = null)
    {
        var (x, z) = Wp(p, q);
        ctx.Props.Add(new Prop(kind, x, z, size, color ?? ctx.Colors.Accent, y));
    }

    void Furnish(SceneContext ctx, Room r)
    {
        int f = r.Floor, s = S(f), pc = (r.P0 + r.P1) / 2, qc = (r.Q0 + r.Q1) / 2;
        bool back = r.Q0 == 1; // the room lies against the back wall (template 0 rooms beyond the hall, the living side)
        switch (r.Kind)
        {
            case "living":
                Furn(ctx, "sofa", 3, 1, 0, 1, f);
                Furn(ctx, "coffee_table", 4, 5, 0, 1, f);
                Furn(ctx, "tv_stand", 1, 16, 1, 0, f);
                Furn(ctx, "bookshelf", _xL - 2, 9, -1, 0, f);
                Thing(ctx, PropKind.Cup, 5, 6, s + 3, 0.3f, ctx.Colors.Light);
                Thing(ctx, PropKind.Plant, 7, 6, s + 3);
                Thing(ctx, PropKind.Lamp, 8, 7, s + 3, 0.5f, ctx.Colors.Light);
                Thing(ctx, PropKind.Plant, _xL - 2, _d - 3, s, 0.8f);
                break;
            case "kitchen":
            {
                int cw = Math.Clamp(r.P1 - r.P0 - 10, 6, 12);
                int cq = back ? r.Q0 : r.Q1 - 3, dq = back ? 1 : -1;
                Furn(ctx, "kitchen_counter", r.P0 + 2, cq, 0, dq, f, cw);
                Furn(ctx, "fridge", r.P0 + 2 + cw, cq, 0, dq, f);
                Table(ctx, f, pc, qc, chairs: 4);
                break;
            }
            case "bath":
                if (back)
                {
                    Furn(ctx, "bathtub", r.P1 - 10, r.Q0, 0, 1, f);
                    Furn(ctx, "sink", r.P1 - 2, r.Q0 + 6, -1, 0, f);
                }
                else
                {
                    Furn(ctx, "bathtub", r.P1 - 10, r.Q1 - 5, 0, -1, f);
                    Furn(ctx, "sink", r.P1 - 2, r.Q0 + 1, -1, 0, f);
                }
                break;
            case "bedroom" when r.P0 == 1:
                Furn(ctx, "bed", 1, 8, 1, 0, f);
                Furn(ctx, "wardrobe", 12, 1, 0, 1, f);
                Thing(ctx, PropKind.Plant, _xL - 2, _d - 3, s, 0.8f);
                break;
            case "bedroom":
                if (back)
                {
                    Furn(ctx, "bed", pc - 3, r.Q0, 0, 1, f);
                    Furn(ctx, "wardrobe", r.P1 - 3, 12, -1, 0, f);
                }
                else
                {
                    Furn(ctx, "bed", pc - 3, r.Q1 - 9, 0, -1, f);
                    Furn(ctx, "wardrobe", r.P1 - 3, 16, -1, 0, f);
                }
                break;
            case "study":
            {
                int sq = back ? r.Q0 : r.Q1 - 2, dq = back ? 1 : -1;
                Furn(ctx, "bookshelf", r.P0 + 2, sq, 0, dq, f);
                Furn(ctx, "bookshelf", r.P0 + 9, sq, 0, dq, f);
                Table(ctx, f, pc, qc, chairs: 1);
                break;
            }
            case "storage":
                Furn(ctx, "bookshelf", r.P0 + 1, r.Q0, 0, 1, f);
                Furn(ctx, "bookshelf", r.P0 + 1, r.Q1 - 2, 0, -1, f);
                break;
        }
    }

    /// <summary>A dining table (or desk) centred at (pc, qc) with chairs on its long sides and things on its top.</summary>
    void Table(SceneContext ctx, int f, int pc, int qc, int chairs)
    {
        int s = S(f);
        Furn(ctx, "dining_table", pc - 4, qc - 3, 0, 1, f);
        var seats = new[] { (pc - 3, qc + 3, -1), (pc + 1, qc + 3, -1), (pc - 3, qc - 6, 1), (pc + 1, qc - 6, 1) };
        foreach (var (p, q, dq) in seats.Take(chairs)) Furn(ctx, "chair", p, q, 0, dq, f);
        Thing(ctx, PropKind.Plant, pc, qc, s + 3);
        Thing(ctx, PropKind.Cup, pc - 2, qc - 1, s + 3, 0.3f, ctx.Colors.Light);
        Thing(ctx, PropKind.Lamp, pc + 2, qc, s + 3, 0.5f, ctx.Colors.Light);
    }

    // ---- the yard ------------------------------------------------------------------------------------------------

    void Yard_(SceneContext ctx)
    {
        var w = ctx.World;
        int fa0 = _hA0 - Yard, fa1 = _hA0 + _l + Yard - 1, fb0 = _hB0 - Yard, fb1 = _hB0 + _d + Yard - 1;
        void Run(int from, int to, bool alongA, int at)
        {
            int len = to - from;
            if (len < 2) return;
            var origin = alongA ? _f.Origin(from, at, len, 1, Ground) : _f.Origin(at, from, 1, len, Ground);
            ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("fence_segment"), origin,
                LocalFrame.Along(alongA ? _f.FacingOf(1, 0) : _f.FacingOf(0, 1)), ctx.Colors, new PlaceOptions(StretchTo: len), ctx.Rng));
        }
        int pathA = AMin(_xL + 3, DoorW), gate = pathA + DoorW / 2 - 3;
        Run(fa0, fa1 + 1, true, fb0);
        Run(fa0, gate, true, fb1);
        Run(gate + 6, fa1 + 1, true, fb1);
        Run(fb0 + 1, fb1, false, fa0);
        Run(fb0 + 1, fb1, false, fa1);

        // neighbours' houses outside the fence, facing this one
        int cb = _hB0 + _d / 2 - 8;
        bool left = ctx.Rng.Next() < 0.5, both = ctx.Rng.Next() < 0.5;
        if (left || both)
            ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("house_shell"), _f.Origin(fa0 - 16, cb, 12, 16, Ground),
                LocalFrame.FrontTo(_f.FacingOf(1, 0)), ctx.Colors, new PlaceOptions(), ctx.Rng));
        if (!left || both)
            ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("house_shell"), _f.Origin(fa1 + 5, cb, 12, 16, Ground),
                LocalFrame.FrontTo(_f.FacingOf(-1, 0)), ctx.Colors, new PlaceOptions(), ctx.Rng));

        // the landmark in the back yard
        if (_landmark == 0)
        {
            int ta = _hA0 + (_mirror ? _l - 13 : 6), tb = _hB0 - 11;
            var tree = PartPlacer.Place(w, PartLibrary.Get("treehouse"), _f.Origin(ta, tb, 7, 7, Ground), LocalFrame.FrontTo(_f.FacingOf(0, 1)),
                ctx.Colors, new PlaceOptions(), ctx.Rng);
            ctx.Structures.Add(tree);
            var (cx, cz) = _f.W(ta + 3, tb + 3);
            ctx.Props.Add(new Prop(PropKind.Tree, cx, cz, 4.5f, ctx.Colors.Accent, FreeAbove(w, cx, cz)));
            ctx.Landmark = new Landmark("treehouse", new BlockPos(cx, Ground + 12, cz));
        }
        else
        {
            int ga = _hA0 + _l / 2 - 6, gb = _hB0 - 12;
            ctx.Structures.Add(PartPlacer.Place(w, PartLibrary.Get("greenhouse"), _f.Origin(ga, gb, 12, 8, Ground),
                LocalFrame.FrontTo(_f.FacingOf(0, 1)), ctx.Colors, new PlaceOptions(), ctx.Rng));
            var (cx, cz) = _f.W(ga + 6, gb + 4);
            ctx.Landmark = new Landmark("greenhouse", new BlockPos(cx, Ground + 5, cz));
        }

        // trees in the yard and beyond the fence
        bool Covered(int x, int z) => ctx.Structures.Any(s => x >= s.Min.X - 2 && x <= s.Max.X + 2 && z >= s.Min.Z - 2 && z <= s.Max.Z + 2);
        for (int b = 2; b < N - 2; b++)
            for (int a = 2; a < N - 2; a++)
            {
                var (x, z) = _f.W(a, b);
                if (ctx.Path[z * N + x] || ctx.Noise.Hash01(x, z, 77) >= 1.0 / 70 || Covered(x, z)) continue;
                if (w.Get(x, Ground, z) != Block.Air || !Block.IsSolid(w.Get(x, Ground - 1, z))) continue;
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, (float)(0.9 + 0.5 * ctx.Noise.Hash01(x, z, 78)), ctx.Colors.Accent));
            }
    }

    /// <summary>First free cell above the highest solid block of a column.</summary>
    static int FreeAbove(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }

    // ---- view ----------------------------------------------------------------------------------------------------

    public SceneView View(SceneContext ctx)
    {
        var (sx, sz) = Wp(SpawnP, SpawnQ);
        var (tx, tz) = Wp(SpawnP, _d - 1);
        var mural = LocalFrame.Spot(Wp(_l - 1, _hq + 1), Wp(_l - 1, _hq + 10), Level[0] + 1, HF(-1, 0), 9, framed: true);
        return new SceneView(new BlockPos(sx, Ground, sz), new BlockPos(tx, Ground + 6, tz), new[] { mural });
    }
}
