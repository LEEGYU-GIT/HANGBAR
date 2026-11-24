using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

/// <summary>
/// A small scene for pipeline tests: gentle hills between y 56 and 70, a pond in the north-west corner, one old
/// stone_steps part, spawn in the middle facing north, one mural candidate (a cliff face cut into a hill).
/// </summary>
public sealed class TestScene(bool withMuralCandidate = true) : IScene
{
    public const int C = SceneContext.Size / 2;
    public Scene Kind => Scene.Mountain;

    public void Shape(SceneContext ctx)
    {
        for (int z = 0; z < SceneContext.Size; z++)
            for (int x = 0; x < SceneContext.Size; x++)
            {
                int i = ctx.Index(x, z);
                ctx.Surface[i] = 56 + (int)Math.Round(14 * ctx.Noise.Sample(x, z, 48, 1));
                if (x > 20 && x < 60 && z > 20 && z < 60)
                {
                    ctx.Surface[i] = 50;
                    ctx.WaterTop[i] = 56;
                }
            }
        // flat spawn yard
        for (int z = C - 4; z <= C + 4; z++)
            for (int x = C - 4; x <= C + 4; x++)
            {
                ctx.Surface[ctx.Index(x, z)] = 60;
                ctx.NoCaves[ctx.Index(x, z)] = true;
            }
        Array.Copy(Slope.Limit(ctx.Surface, Pins(ctx), SceneContext.Size, 2), ctx.Surface, ctx.Surface.Length);
    }

    static int[] Pins(SceneContext ctx)
    {
        var p = Enumerable.Repeat(Slope.NoPin, SceneContext.Size * SceneContext.Size).ToArray();
        for (int z = C - 4; z <= C + 4; z++)
            for (int x = C - 4; x <= C + 4; x++) p[ctx.Index(x, z)] = 60;
        return p;
    }

    public void Build(SceneContext ctx)
    {
        int x0 = C + 30, z0 = C + 30, ground = ctx.Surface[ctx.Index(x0, z0)];
        var steps = PartPlacer.Place(ctx.World, PartLibrary.Get("stone_steps"), new BlockPos(x0, ground, z0), Rotation.R0,
            ctx.Colors, new PlaceOptions(), ctx.Rng);
        ctx.Structures.Add(steps);
        ctx.MarkOld(steps);
        ctx.Landmark = new Landmark("test", new BlockPos(x0, ground, z0));
    }

    public SceneView View(SceneContext ctx)
    {
        var spawn = new BlockPos(C, ctx.Surface[ctx.Index(C, C)], C);
        var target = new BlockPos(C, ctx.Surface[ctx.Index(C, C - 30)], C - 30);
        var candidates = withMuralCandidate
            ? new[] { new MuralSpot(new BlockPos(C + 60, ctx.Surface[ctx.Index(C + 60, C + 70)], C + 70), Facing.South, 24, 18, false) }
            : Array.Empty<MuralSpot>();
        return new SceneView(spawn, target, candidates);
    }
}
