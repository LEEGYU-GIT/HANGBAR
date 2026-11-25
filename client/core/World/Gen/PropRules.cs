namespace Hangbar.Core.World.Gen;

/// <summary>
/// Rules every scene's props keep, applied after the scene has placed them (v2 design §6-8):
/// big props stay out of the first view (from the camera behind the spawn to the view target), and tree crowns
/// never grow into a structure. Tree sizes match game/src/PropsView.cs: crown centre 2 m × scale above the feet,
/// radius 0.9 m × scale.
/// </summary>
public static class PropRules
{
    /// <summary>Cells the third-person camera sits behind the spawn (its 4.5 m spring arm, rounded up).</summary>
    public const double CameraBack = 20;

    static readonly PropKind[] Big = { PropKind.Tree, PropKind.Bush, PropKind.Parasol, PropKind.Bench, PropKind.Rock };

    /// <summary>How far (in columns) a prop's centre must stay from the line of the first view.</summary>
    public static double ClearRadius(Prop p) => 2 + 4 * p.Scale;

    /// <summary>A tree's crown as an inclusive block box, for a tree whose feet stand at <paramref name="y"/>.</summary>
    public static (BlockPos Min, BlockPos Max) Crown(Prop tree, int y)
    {
        double s = tree.Scale, r = 3.6 * s, cy = y + 8 * s;
        return (new BlockPos((int)Math.Floor(tree.X + 0.5 - r), (int)Math.Floor(cy - r), (int)Math.Floor(tree.Z + 0.5 - r)),
                new BlockPos((int)Math.Floor(tree.X + 0.5 + r), (int)Math.Floor(cy + r), (int)Math.Floor(tree.Z + 0.5 + r)));
    }

    /// <summary>Drops the props that break a rule: big ones in the first view, trees whose crown meets a structure.</summary>
    public static void Apply(SceneContext ctx, SceneView view)
    {
        double dx = view.Target.X - view.Spawn.X, dz = view.Target.Z - view.Spawn.Z, len = Math.Sqrt(dx * dx + dz * dz);
        double fx = len > 0 ? dx / len : 0, fz = len > 0 ? dz / len : -1;
        var camera = (X: view.Spawn.X + 0.5 - fx * CameraBack, Z: view.Spawn.Z + 0.5 - fz * CameraBack);
        var target = (X: view.Target.X + 0.5, Z: view.Target.Z + 0.5);
        ctx.Props.RemoveAll(p =>
            (Array.IndexOf(Big, p.Kind) >= 0 && DistanceToSegment(p.X + 0.5, p.Z + 0.5, camera, target) < ClearRadius(p)) ||
            (p.Kind == PropKind.Tree && CrownHitsStructure(ctx, p)));
    }

    static bool CrownHitsStructure(SceneContext ctx, Prop tree)
    {
        var (min, max) = Crown(tree, tree.Y >= 0 ? tree.Y : TopFree(ctx.World, tree.X, tree.Z));
        return ctx.Structures.Any(s => min.X <= s.Max.X && max.X >= s.Min.X && min.Y <= s.Max.Y && max.Y >= s.Min.Y &&
                                       min.Z <= s.Max.Z && max.Z >= s.Min.Z);
    }

    static double DistanceToSegment(double px, double pz, (double X, double Z) a, (double X, double Z) b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, len2 = dx * dx + dz * dz;
        double t = len2 == 0 ? 0 : Math.Clamp(((px - a.X) * dx + (pz - a.Z) * dz) / len2, 0, 1);
        double cx = a.X + t * dx - px, cz = a.Z + t * dz - pz;
        return Math.Sqrt(cx * cx + cz * cz);
    }

    static int TopFree(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }
}
