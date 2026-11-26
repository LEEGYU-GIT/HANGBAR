namespace Hangbar.Core.World.Gen;

/// <summary>
/// Alley (v2 part 2 plan Task 6). A hillside of five terraces, four blocks apart. A narrow cobbled alley runs along the
/// foot of each terrace with low house shells facing it, their backs to the next terrace's retaining wall; a winding
/// main alley climbs through all of them with steps of one block (stairs). One small house can be entered; its back
/// room holds a framed photo. The landmark is a little square with an old tree, a covered well or a viewpoint on the
/// top terrace. The first view looks from the bottom of the main alley up the hill.
/// </summary>
/// <remarks>Local frame: b climbs the hill. Terrace k (0..4) covers b [16 + 32k, 48 + 32k) at height 57 + 4k.</remarks>
public sealed class AlleyScene : IScene
{
    const int N = SceneContext.Size, Base = 57, Rise = 4, Band = 32, First = 16, Terraces = 5, AlleyW = 10, MainHalf = 6;
    const int HouseW = 16, HouseD = 12, SmallL = 18, SmallD = 18;

    public Scene Kind => Scene.Alley;

    LocalFrame _f;
    int _aM, _landmark, _smallK, _smallA, _sqK, _sqA;
    double _wig;
    MuralSpot? _mural;

    static int TerraceOf(int b) => Math.Clamp((b - First) / Band, 0, Terraces - 1);
    static int HeightOf(int k) => Base + Rise * k;
    static int BandStart(int k) => First + Band * k;
    int Main(int b) => (int)Math.Round(_aM + _wig * Math.Sin(b / 26.0));
    bool InMain(int a, int b) => Math.Abs(a - Main(b)) <= MainHalf;

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _aM = rng.NextInt(80, 113);
        _wig = 2 + 3 * rng.Next();
        _landmark = rng.NextInt(0, 3);
        _smallK = rng.NextInt(1, 4);
        _smallA = rng.Next() < 0.5 ? rng.NextInt(20, 50) : rng.NextInt(130, 160);
        _sqK = _landmark == 2 ? Terraces - 1 : (_smallK + 1 + rng.NextInt(0, 2)) % (Terraces - 1);
        _sqA = _smallA < 96 ? rng.NextInt(130, 150) : rng.NextInt(26, 46);

        for (int b = 0; b < N; b++)
            for (int a = 0; a < N; a++)
            {
                int i = _f.I(a, b), k = TerraceOf(b), y = b < First ? Base : HeightOf(k);
                ctx.Surface[i] = y;
                ctx.NoCaves[i] = true;
                ctx.TopMaterial[i] = Material.Grass;
                int inBand = b - BandStart(k);
                if (b >= First && inBand < AlleyW) Alley(ctx, i);          // the alley at the foot of each terrace
                if (b < First) Alley(ctx, i);                              // and the lane at the bottom
            }
        // the main alley climbs through every terrace; the four cells below each terrace step up one block each
        for (int b = 4; b < BandStart(Terraces - 1) + Band - 2; b++)
            for (int a = Main(b) - MainHalf; a <= Main(b) + MainHalf; a++)
            {
                int i = _f.I(a, b), next = TerraceOf(b + 4);
                Alley(ctx, i);
                int start = BandStart(next);
                if (b < start && b >= start - 4 && next > 0)
                {
                    ctx.Surface[i] = HeightOf(next - 1) + (b - (start - 4)) + 1;
                    ctx.Stairs[i] = true;
                }
            }
    }

    static void Alley(SceneContext ctx, int i)
    {
        ctx.Path[i] = true;
        ctx.TopMaterial[i] = Material.Stone;
    }

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        // retaining walls: the stone face of each terrace above the alley below it
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        for (int k = 1; k < Terraces; k++)
            for (int a = 0; a < N; a++)
            {
                int b = BandStart(k) - 1;
                if (InMain(a, b)) continue;
                var (x, z) = _f.W(a, b + 1);
                for (int y = HeightOf(k - 1); y < HeightOf(k); y++) w.Set(x, y, z, stone);
            }

        // houses along each terrace's alley, facing it; gaps between them, none on the main alley
        for (int k = 0; k < Terraces; k++)
        {
            int b0 = BandStart(k) + AlleyW + 1, y = HeightOf(k);
            for (int a = 8; a + HouseW < N - 8; a += HouseW + 6)
            {
                if (Math.Abs(a + HouseW / 2 - Main(b0 + HouseD / 2)) < HouseW / 2 + MainHalf + 3) continue;
                if (k == _smallK && a + HouseW > _smallA - 4 && a < _smallA + SmallL + 4) continue;
                if (k == _sqK && a + HouseW > _sqA - 14 && a < _sqA + 14) continue;
                SceneTools.Place(ctx, "house_shell", _f.Origin(a, b0, HouseW, HouseD, y), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
            }
        }

        // the small house you can enter: a front room and a back room with the photo
        var plan = new InteriorPlan(SmallL, SmallD, Material.Brick,
            new[] { new InteriorRoom("front_room", 1, SmallL - 1, 1, 7, Material.Wood), new InteriorRoom("back_room", 1, SmallL - 1, 8, SmallD - 1, Material.Wood) },
            new[] { new InteriorDoor(false, 0, 7), new InteriorDoor(false, 7, 12) }); // doors out of line: no view through both
        var small = Interior.Build(ctx, _f, _smallA, BandStart(_smallK) + AlleyW + 1, plan);
        small.Furn("dining_table", 3, 2, 0, 1);
        small.Furn("chair", 4, 5, 0, 1);
        small.Furn("bed", SmallL - 10, 9, -1, 0);
        small.Furn("wardrobe", 2, 10, 1, 0);
        small.Thing(PropKind.Cup, 5, 3, 3, 0.3f, ctx.Colors.Light);
        _mural = small.Mural(false, SmallD - 1, 3, 8, 0, -1, height: 6);

        Landmark(ctx);
        // pots of flowers along the houses' fronts, and a few trees in the gaps
        SceneTools.Scatter(ctx, (x, z, mat, r) =>
        {
            if (mat == Material.Grass && r < 1.0 / 25) return new Prop(PropKind.Plant, x, z, 0.6f, ctx.Noise.Hash01(x, z, 79) < 0.5 ? ctx.Colors.Accent : ctx.Colors.Light);
            if (mat == Material.Grass && r is >= 0.5 and < 0.5 + 1.0 / 70) return new Prop(PropKind.Tree, x, z, 0.9f, ctx.Colors.Accent);
            return null;
        }, margin: 1);
    }

    void Landmark(SceneContext ctx)
    {
        var w = ctx.World;
        int b0 = BandStart(_sqK) + AlleyW + 1, y = HeightOf(_sqK);
        uint stone = Block.Solid(ctx.ColorOf(Material.Stone), Material.Stone);
        uint wood = Block.Solid(ctx.ColorOf(Material.Wood), Material.Wood);
        void Fill(int a0, int bb0, int wa, int wb, int ya, int yb, uint blk)
        {
            for (int b = bb0; b < bb0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++)
                {
                    var (x, z) = _f.W(a, b);
                    for (int yy = ya; yy <= yb; yy++) w.Set(x, yy, z, blk);
                }
        }
        // a paved square in front of the landmark
        for (int b = b0; b < b0 + 18; b++)
            for (int a = _sqA - 12; a < _sqA + 12; a++)
            {
                var (x, z) = _f.W(a, b);
                w.Set(x, y - 1, z, stone);
            }
        switch (_landmark)
        {
            case 0:
            {
                var (x, z) = _f.W(_sqA, b0 + 9);
                ctx.Props.Add(new Prop(PropKind.Tree, x, z, 3.5f, ctx.Colors.Accent));
                Fill(_sqA - 2, b0 + 7, 5, 1, y, y, stone); Fill(_sqA - 2, b0 + 11, 5, 1, y, y, stone);
                Fill(_sqA - 2, b0 + 8, 1, 3, y, y, stone); Fill(_sqA + 2, b0 + 8, 1, 3, y, y, stone);
                ctx.Structures.Add(_f.Box("old_tree_square", _sqA - 2, b0 + 7, 5, 5, y, y));
                ctx.Landmark = new Landmark("old_tree_square", Center(_sqA, b0 + 9, y + 10));
                break;
            }
            case 1:
            {
                Fill(_sqA - 3, b0 + 6, 7, 7, y, y + 2, stone);
                Fill(_sqA - 2, b0 + 7, 5, 5, y - 2, y + 2, Block.Air);
                Fill(_sqA - 2, b0 + 7, 5, 5, y - 4, y - 3, Block.Water);
                Fill(_sqA - 3, b0 + 6, 1, 1, y + 3, y + 10, wood); Fill(_sqA + 3, b0 + 12, 1, 1, y + 3, y + 10, wood);
                Fill(_sqA - 3, b0 + 12, 1, 1, y + 3, y + 10, wood); Fill(_sqA + 3, b0 + 6, 1, 1, y + 3, y + 10, wood);
                Fill(_sqA - 4, b0 + 5, 9, 9, y + 11, y + 11, Block.Solid(ctx.Colors.Accent, Material.Wood));
                ctx.Structures.Add(_f.Box("well", _sqA - 4, b0 + 5, 9, 9, y - 4, y + 11));
                ctx.Landmark = new Landmark("well", Center(_sqA, b0 + 9, y + 6));
                break;
            }
            default:
            {
                // the viewpoint: a railing at the top terrace's edge, benches looking down the hill
                int edge = BandStart(_sqK);
                SceneTools.Place(ctx, "fence_segment", _f.Origin(_sqA - 12, edge, 24, 1, y), LocalFrame.Along(_f.FacingOf(1, 0)),
                    new PlaceOptions(StretchTo: 24));
                for (int a = _sqA - 8; a <= _sqA + 8; a += 8)
                {
                    var (x, z) = _f.W(a, edge + 3);
                    ctx.Props.Add(new Prop(PropKind.Bench, x, z, 1f, ctx.ColorOf(Material.Wood), y));
                }
                ctx.Landmark = new Landmark("viewpoint", Center(_sqA, edge + 2, y + 4));
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
        var (sx, sz) = _f.W(Main(First + 2), First + 2);
        var spawn = new BlockPos(sx, ctx.Surface[sz * N + sx], sz);
        var target = Center(Main(40), 40, HeightOf(0) + 8);
        return new SceneView(spawn, target, _mural is { } m ? new[] { m } : Array.Empty<MuralSpot>());
    }
}
