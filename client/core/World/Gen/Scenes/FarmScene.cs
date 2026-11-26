namespace Hangbar.Core.World.Gen;

/// <summary>
/// Farm (v2 part 2 plan Task 3). Flat farmland: a dirt lane in front, a fenced farmyard with a gate, a red barn you
/// can walk into (stairs to a hay loft whose back wall holds a framed photo), a farmhouse shell; fields round the yard
/// in furrows of soil and rows of crops, dirt lanes between them. The landmark is a grain silo, a windmill behind the
/// yard or scarecrows standing in the fields. The first view looks from the lane through the gate at the barn.
/// </summary>
/// <remarks>Local frame: b runs from the lane (small b) into the farm.</remarks>
public sealed class FarmScene : IScene
{
    const int N = SceneContext.Size, Ground = 57, LaneB = 40, YardB0 = 56, YardB1 = 122;

    public Scene Kind => Scene.Farm;

    LocalFrame _f;
    int _sA, _landmark;
    readonly List<(int A0, int B0, int Wa, int Wb)> _fields = new();
    PlacedPart? _barn;
    MuralSpot? _loft;

    public void Shape(SceneContext ctx)
    {
        var rng = ctx.Rng;
        _f = new LocalFrame(rng.NextInt(0, 4));
        _sA = rng.NextInt(84, 109);
        _landmark = rng.NextInt(0, 3);
        Array.Fill(ctx.Surface, Ground);

        void Lane(int a0, int a1, int b0, int b1)
        {
            for (int b = b0; b < b1; b++)
                for (int a = a0; a < a1; a++)
                {
                    int i = _f.I(Math.Clamp(a, 0, N - 1), Math.Clamp(b, 0, N - 1));
                    ctx.Path[i] = true;
                    ctx.TopMaterial[i] = Material.Soil;
                }
        }
        Lane(0, N, LaneB - 3, LaneB + 3);                       // the lane in front
        Lane(_sA - 3, _sA + 3, LaneB + 3, YardB0);              // to the gate
        Lane(_sA - 3, _sA + 3, YardB0, 96);                     // through the yard to the barn door
        int left = _sA - 34, right = _sA + 31;
        Lane(left, left + 4, LaneB + 3, N - 16);                // lanes beside the yard, out to the back fields
        Lane(right, right + 4, LaneB + 3, N - 16);
        Lane(16, N - 16, 124, 128);                             // a lane across behind the yard

        // fields in the four corners round the yard and one behind it (unless the windmill stands there)
        void Field(int a0, int b0, int wa, int wb) { if (wa >= 20 && wb >= 16) _fields.Add((a0, b0, wa, wb)); }
        Field(18, 52, left - 22, 34); Field(18, 90, left - 22, 32);
        Field(right + 6, 52, N - 18 - right - 6, 34); Field(right + 6, 90, N - 18 - right - 6, 32);
        Field(18, 132, left - 22, 40); Field(right + 6, 132, N - 18 - right - 6, 40);
        if (_landmark != 1) Field(left + 6, 132, right - left - 8, 40);
        foreach (var (a0, b0, wa, wb) in _fields)
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0; a < a0 + wa; a++)
                {
                    int i = _f.I(a, b);
                    ctx.TopMaterial[i] = (b - b0) % 4 < 2 ? Material.Soil : Material.Grass; // furrow, crop row
                    ctx.NoCaves[i] = true;
                }
        for (int b = LaneB - 4; b < YardB1 + 2; b++)
            for (int a = _sA - 34; a < _sA + 36; a++) ctx.NoCaves[_f.I(a, b)] = true;
    }

    public void Build(SceneContext ctx)
    {
        var w = ctx.World;
        // the farmyard: a fence with a gate facing the lane
        SceneTools.FenceRing(ctx, _f, _sA - 30, YardB0, _sA + 29, YardB1, Ground, (0, _sA, 8));
        _barn = SceneTools.Place(ctx, "barn", _f.Origin(_sA - 10, 96, 20, 16, Ground), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
        var ma = _barn.Anchors["mural_a"];
        var mb = _barn.Anchors["mural_b"];
        _loft = LocalFrame.Spot((ma.X, ma.Z), (mb.X, mb.Z), ma.Y, _f.FacingOf(0, -1), 6, framed: true);
        SceneTools.Place(ctx, "house_shell", _f.Origin(_sA + 10, 64, 16, 12, Ground), LocalFrame.FrontTo(_f.FacingOf(0, -1)));

        foreach (var (a0, b0, wa, wb) in _fields)
            ctx.Structures.Add(_f.Box("field", a0, b0, wa, wb, Ground - 1, Ground));
        // crops on the crop rows
        foreach (var (a0, b0, wa, wb) in _fields)
            for (int b = b0; b < b0 + wb; b++)
                for (int a = a0 + 1; a < a0 + wa - 1; a += 2)
                    if ((b - b0) % 4 == 2)
                    {
                        var (x, z) = _f.W(a, b);
                        ctx.Props.Add(new Prop(PropKind.Plant, x, z, 0.45f, ctx.Noise.Hash01(a, b0, 31) < 0.5 ? ctx.Colors.Accent : 0xC9B24A, Ground));
                    }

        switch (_landmark)
        {
            case 0:
                SceneTools.Place(ctx, "silo", _f.Origin(_sA - 26, 100, 7, 7, Ground), Rotation.R0);
                ctx.Landmark = new Landmark("silo", Center(_sA - 23, 103, Ground + 14));
                break;
            case 1:
                SceneTools.Place(ctx, "windmill", _f.Origin(_sA - 10, 140, 21, 9, Ground), LocalFrame.FrontTo(_f.FacingOf(0, -1)));
                ctx.Landmark = new Landmark("windmill", Center(_sA, 144, Ground + 16));
                break;
            default:
                foreach (var (a0, b0, wa, wb) in _fields.Take(3))
                {
                    int a = a0 + wa / 2, b = b0 + wb / 2 + 1 - (wb / 2 + 1) % 4; // on a furrow
                    SceneTools.Place(ctx, "scarecrow", _f.Origin(a - 2, b, 5, 1, Ground), LocalFrame.Along(_f.FacingOf(1, 0)));
                }
                var first = _fields[0];
                ctx.Landmark = new Landmark("scarecrows", Center(first.A0 + first.Wa / 2, first.B0 + first.Wb / 2, Ground + 6));
                break;
        }

        SceneTools.Scatter(ctx, (x, z, mat, r) =>
        {
            float size = (float)(0.7 + 0.5 * ctx.Noise.Hash01(x, z, 78));
            if (mat == Material.Grass && r < 1.0 / 90) return new Prop(PropKind.Tree, x, z, size + 0.3f, ctx.Colors.Accent);
            if (mat == Material.Grass && r is >= 0.5 and < 0.5 + 1.0 / 140) return new Prop(PropKind.Bush, x, z, size, ctx.Colors.Accent);
            return null;
        }, margin: 3);
    }

    BlockPos Center(int a, int b, int y)
    {
        var (x, z) = _f.W(a, b);
        return new BlockPos(x, y, z);
    }

    public SceneView View(SceneContext ctx)
    {
        var (sx, sz) = _f.W(_sA, LaneB);
        var spawn = new BlockPos(sx, ctx.Surface[sz * N + sx], sz);
        var target = Center(_sA, 95, Ground + 16); // just in front of the barn face
        var candidates = new List<MuralSpot>();
        if (_loft is { } loft) candidates.Add(loft);
        // the farmhouse's back wall, facing away from the lane
        candidates.Add(LocalFrame.Spot(_f.W(_sA + 11, 75), _f.W(_sA + 24, 75), Ground + 1, _f.FacingOf(0, 1), 9));
        return new SceneView(spawn, target, candidates);
    }
}
