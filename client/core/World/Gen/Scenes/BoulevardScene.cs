namespace Hangbar.Core.World.Gen;

/// <summary>
/// City boulevard (v2 part 2 plan Task 5). Flat ground: a six-lane boulevard with a planted median crosses the map,
/// a four-lane street crosses it at one intersection, crosswalks on all four arms, raised brick sidewalks, two traffic
/// lights, trees along the sidewalks, rows of outside-only buildings on both sides, and one shop you can walk into
/// (its storeroom holds a framed photo). The landmark stands at the far end of the median: a clock tower, a billboard
/// or a monument gate. The first view looks from the sidewalk down the boulevard.
/// </summary>
/// <remarks>Local frame: a runs along the boulevard, b across it (centre b = 100).</remarks>
public sealed class BoulevardScene : IScene
{
    const int N = SceneContext.Size, Ground = 57, Centre = 100, Lanes = 6, Walk = 12, CrossLanes = 4;
    const int ShopL = 28, ShopD = 24;

    public Scene Kind => Scene.Boulevard;

    LocalFrame _f, _g;
    int _aC, _landmark, _shopA;
    RoadInfo _road = null!, _cross = null!;
    MuralSpot? _mural;
    BlockPos _target; // just in front of the landmark's near face

    int Half => Lanes * Streets.LaneWidth / 2;
    int CrossHalf => CrossLanes * Streets.LaneWidth / 2;

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _g = new LocalFrame((_f.Side + 1) % 4); // g(a', b') = f(N − 1 − b', a'): roads along g's a run along f's b
        _aC = rng.NextInt(80, 113);
        _landmark = rng.NextInt(0, 3);
        Array.Fill(ctx.Surface, Ground);

        _cross = Streets.Road(ctx, _g, 0, N, N - 1 - _aC, CrossLanes, Walk);
        _road = Streets.Road(ctx, _f, 0, N, Centre, Lanes, Walk);
        // the intersection: one carriageway where the two roads meet
        for (int b = _road.SidewalkFrom; b < _road.SidewalkTo; b++)
            for (int a = _aC - CrossHalf - Walk; a < _aC + CrossHalf + Walk; a++)
            {
                bool inBoulevard = b >= _road.CarriageFrom && b < _road.CarriageTo;
                bool inCross = a >= _aC - CrossHalf && a < _aC + CrossHalf;
                if (!inBoulevard && !inCross) continue;
                int i = _f.I(a, b);
                ctx.Surface[i] = Ground;
                ctx.TopMaterial[i] = Material.Concrete;
                ctx.Path[i] = false;
            }
        // the planted median, one block up, broken at the intersection and the crosswalks
        for (int a = 0; a < N; a++)
        {
            if (Math.Abs(a - _aC) < CrossHalf + Walk + 10) continue;
            for (int b = Centre - 4; b < Centre + 4; b++)
            {
                int i = _f.I(a, b);
                ctx.Surface[i] = Ground + 1;
                ctx.TopMaterial[i] = Material.Grass;
                ctx.Path[i] = true;
            }
        }
        foreach (int a in new[] { _aC - CrossHalf - Walk - 6, _aC + CrossHalf + Walk + 5 })
            Streets.Crosswalk(ctx, _f, a, _road);
        foreach (int b in new[] { _road.SidewalkFrom - 7, _road.SidewalkTo + 6 })
            Streets.Crosswalk(ctx, _g, b, _cross);

        _shopA = rng.NextInt(_aC + CrossHalf + Walk + 6, N - 8 - ShopL);
        for (int i = 0; i < N * N; i++) ctx.NoCaves[i] = true; // a city floor: no cave mouths
    }

    public void Build(SceneContext ctx)
    {
        var rng = ctx.Rng;
        // the shop, on the far side, its door on the sidewalk
        var plan = new InteriorPlan(ShopL, ShopD, Material.Brick,
            new[]
            {
                new InteriorRoom("shop", 1, ShopL - 1, 1, 15, Material.Tile),
                new InteriorRoom("storage", 1, 13, 16, ShopD - 1, Material.Concrete),
                new InteriorRoom("office", 14, ShopL - 1, 16, ShopD - 1, Material.Wood),
            },
            new[] { new InteriorDoor(false, 0, 12), new InteriorDoor(false, 15, 4), new InteriorDoor(false, 15, 19) });
        var shop = Interior.Build(ctx, _f, _shopA, _road.SidewalkTo, plan);
        shop.Furn("bookshelf", 2, 1, 0, 1);
        shop.Furn("bookshelf", 2, 8, 0, 1);
        shop.Furn("bookshelf", ShopL - 8, 8, 0, 1);
        shop.Furn("kitchen_counter", ShopL - 12, 2, 0, 1, stretch: 8);
        shop.Furn("bookshelf", 2, ShopD - 3, 0, -1);
        shop.Furn("dining_table", 17, 18, 0, 1);
        shop.Furn("chair", 19, 21, 0, -1);
        shop.Thing(PropKind.Plant, ShopL - 3, 13, 0, 0.8f);
        shop.Thing(PropKind.Lamp, 20, 20, 3, 0.5f, ctx.Colors.Light);
        _mural = shop.Mural(false, ShopD - 1, 3, 10, 0, -1, height: 7);

        // rows of outside-only buildings on both sides, leaving the cross street and the shop's plot free
        foreach (int side in new[] { -1, 1 })
        {
            int a = 6;
            while (a < N - 14)
            {
                int width = rng.NextInt(12, 23), depth = rng.NextInt(20, 27), floors = rng.NextInt(3, 7);
                if (a + width > N - 6) break;
                bool crossing = a + width > _aC - CrossHalf - Walk - 2 && a < _aC + CrossHalf + Walk + 2;
                bool shopPlot = side > 0 && a + width > _shopA - 2 && a < _shopA + ShopL + 2;
                if (crossing) { a = _aC + CrossHalf + Walk + 2; continue; }
                if (shopPlot) { a = _shopA + ShopL + 3; continue; }
                int b0 = side < 0 ? _road.SidewalkFrom - depth : _road.SidewalkTo;
                Streets.Building(ctx, _f, a, b0, width, depth, floors, (0, side < 0 ? 1 : -1),
                    rng.Next() < 0.5 ? Material.Brick : Material.Concrete);
                a += width + rng.NextInt(2, 5);
            }
        }

        // traffic lights on the far corners, arms over the boulevard
        foreach (int a in new[] { _aC - CrossHalf - Walk + 1, _aC + CrossHalf + Walk - 2 })
            SceneTools.Place(ctx, "traffic_light", _f.Origin(a, _road.CarriageTo + 1, 1, 9, Ground + 1), LocalFrame.Along(_f.FacingOf(0, -1)));

        // trees along the outer edge of the sidewalks
        foreach (int b in new[] { _road.SidewalkFrom + 1, _road.SidewalkTo - 2 })
            for (int a = 10; a < N - 10; a += 20)
            {
                if (Math.Abs(a - _aC) < CrossHalf + Walk + 4 || (b > Centre && a > _shopA - 4 && a < _shopA + ShopL + 4)) continue;
                var (x, z) = _f.W(a, b);
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, 1.0f, ctx.Colors.Accent, Ground + 1));
            }
        Landmark(ctx);
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        uint metal = Block.Solid(ctx.ColorOf(Material.Metal), Material.Metal);
        uint face = Block.Solid(ctx.Colors.Light, Material.Tile);
        int y0 = Ground + 1;
        void Fill(int a0, int b0, int wa, int wb, int ya, int yb, uint blk)
        {
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++)
                {
                    var (x, z) = _f.W(a, b);
                    for (int y = ya; y <= yb; y++) w.Set(x, y, z, blk);
                }
        }
        switch (_landmark)
        {
            case 0:
                Fill(160, Centre - 4, 8, 8, y0, y0 + 52, stone);
                Fill(159, Centre - 1, 1, 2, y0 + 40, y0 + 45, face);   // clock faces on the two ends
                Fill(168, Centre - 1, 1, 2, y0 + 40, y0 + 45, face);
                Fill(161, Centre - 3, 6, 6, y0 + 53, y0 + 56, metal);
                ctx.Structures.Add(_f.Box("clock_tower", 159, Centre - 4, 10, 8, y0, y0 + 56));
                ctx.Landmark = new Landmark("clock_tower", Center(164, Centre, y0 + 42));
                _target = Center(157, Centre, y0 + 42);
                break;
            case 1:
                Fill(163, Centre - 1, 2, 2, y0, y0 + 30, metal);
                Fill(163, Centre - 14, 2, 28, y0 + 30, y0 + 44, Block.Solid(ctx.Colors.Accent, Material.Cloth));
                ctx.Structures.Add(_f.Box("billboard", 163, Centre - 14, 2, 28, y0, y0 + 44));
                ctx.Landmark = new Landmark("billboard", Center(164, Centre, y0 + 37));
                _target = Center(161, Centre, y0 + 37);
                break;
            default:
                Fill(150, Centre - 4, 4, 8, y0, y0 + 32, stone);
                Fill(168, Centre - 4, 4, 8, y0, y0 + 32, stone);
                Fill(150, Centre - 4, 22, 8, y0 + 28, y0 + 34, stone);
                ctx.Structures.Add(_f.Box("monument_gate", 150, Centre - 4, 22, 8, y0, y0 + 34));
                ctx.Landmark = new Landmark("monument_gate", Center(160, Centre, y0 + 31));
                _target = Center(148, Centre, y0 + 31);
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
        var (sx, sz) = _f.W(20, _road.SidewalkFrom + Walk / 2);
        var spawn = new BlockPos(sx, Ground + 1, sz);
        var target = _target;
        return new SceneView(spawn, target, _mural is { } m ? new[] { m } : Array.Empty<MuralSpot>());
    }
}
