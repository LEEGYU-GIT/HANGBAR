using System.Diagnostics;
using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

/// <summary>Checks every v2 scene shares: walking, props, triangle budget, mural rules, variety, speed.</summary>
public static class SceneKit
{
    public const int N = SceneContext.Size;
    public const int TriangleBudget = 450_000;

    public static GeneratedWorld Make(Scene s, ulong seed, PixelImage? photo = null)
    {
        var p = photo ?? WorldGeneratorTests.Photo;
        return WorldGenerator.Generate(s, Palette.Extract(p), WorldGeneratorTests.Calm, p, seed, Horizon.Estimate(p));
    }

    public static double WalkableShare(GeneratedWorld g) =>
        Walkability.ReachColumns(g.World, g.Spawn).Count(b => b) / (double)(N * N);

    /// <summary>Every prop stands on a solid block with its own cell free (a float may stand on water).</summary>
    public static void PropsStandOnSomething(GeneratedWorld g)
    {
        foreach (var p in g.Props)
        {
            int y = p.Y >= 0 ? p.Y : TopFree(g.World, p.X, p.Z);
            uint below = g.World.Get(p.X, y - 1, p.Z), at = g.World.Get(p.X, y, p.Z);
            bool floatOnWater = p.Kind == PropKind.Float && Block.IsWater(below);
            Assert.True(Block.IsSolid(below) || floatOnWater, $"{p.Kind} at {p.X},{y},{p.Z} has nothing under it");
            Assert.False(Block.IsSolid(at), $"{p.Kind} at {p.X},{y},{p.Z} is inside a block");
        }
    }

    /// <summary>First free cell above the highest solid block of a column.</summary>
    public static int TopFree(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }

    public static int Triangles(VoxelWorld w)
    {
        int tris = 0;
        for (int cy = 0; cy < w.ChunksY; cy++)
            for (int cz = 0; cz < w.ChunksZ; cz++)
                for (int cx = 0; cx < w.ChunksX; cx++)
                {
                    var m = ChunkMesher.Build(w, cx, cy, cz);
                    tris += m.Opaque.TriangleCount + m.Glass.TriangleCount + m.Water.TriangleCount;
                }
        return tris;
    }

    public static void MuralRulesHold(Scene s, int seeds = 10)
    {
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var g = Make(s, seed);
            Assert.True(HiddenMural.IsHidden(g.World, g.Spawn, g.Mural), $"seed {seed}: mural visible from the spawn");
            Assert.True(HiddenMural.Distance(g.Spawn, g.Mural) >= HiddenMural.MinDistance, $"seed {seed}: mural too close");
            Assert.True(HiddenMural.IsReachable(Walkability.ReachCells(g.World, g.Spawn), g.Mural), $"seed {seed}: mural out of reach");
        }
    }

    /// <summary>Share of columns whose top height differs between two worlds.</summary>
    public static double SurfaceDifference(GeneratedWorld a, GeneratedWorld b) =>
        Enumerable.Range(0, N * N).Count(i => a.Surface.Height[i] != b.Surface.Height[i]) / (double)(N * N);

    public static long GenerationMilliseconds(Scene s)
    {
        Make(s, 999); // warm up the JIT
        var sw = Stopwatch.StartNew();
        Make(s, 1234);
        return sw.ElapsedMilliseconds;
    }

    public static bool SeesFromSpawn(GeneratedWorld g, (double X, double Y, double Z) target) =>
        Sight.Clear(g.World, (g.Spawn.X + 0.5, g.Spawn.Y + 6.4, g.Spawn.Z + 0.5), target);

    /// <summary>Godot-style facing (forward = (−sin, 0, −cos)) toward a column, in degrees.</summary>
    public static double FacingTo(BlockPos from, int x, int z) => Math.Atan2(-(x - from.X), -(z - from.Z)) * 180 / Math.PI;

    public static double AngleBetween(double a, double b)
    {
        double d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }
}
