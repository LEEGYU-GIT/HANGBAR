using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

/// <summary>Rules every scene's props keep (v2 design §6-8 first view; round props never pass through blocks).</summary>
public class PropRulesTests
{
    static readonly PropKind[] Big = { PropKind.Tree, PropKind.Bush, PropKind.Parasol, PropKind.Bench, PropKind.Rock };

    static double DistanceToSegment(double px, double pz, (double X, double Z) a, (double X, double Z) b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, len2 = dx * dx + dz * dz;
        double t = len2 == 0 ? 0 : Math.Clamp(((px - a.X) * dx + (pz - a.Z) * dz) / len2, 0, 1);
        double cx = a.X + t * dx - px, cz = a.Z + t * dz - pz;
        return Math.Sqrt(cx * cx + cz * cz);
    }

    [Theory]
    [InlineData(Scene.Beach)]
    [InlineData(Scene.Pool)]
    [InlineData(Scene.House)]
    [InlineData(Scene.Mountain)]
    public void Big_props_keep_out_of_the_first_view(Scene s)
    {
        var g = SceneKit.Make(s, 7);
        double yaw = g.FacingDegrees * Math.PI / 180, fx = -Math.Sin(yaw), fz = -Math.Cos(yaw);
        var camera = (g.Spawn.X + 0.5 - fx * PropRules.CameraBack, g.Spawn.Z + 0.5 - fz * PropRules.CameraBack);
        var target = (g.Target.X + 0.5, g.Target.Z + 0.5);
        foreach (var p in g.Props.Where(p => Big.Contains(p.Kind)))
        {
            double d = DistanceToSegment(p.X + 0.5, p.Z + 0.5, camera, target);
            Assert.True(d >= PropRules.ClearRadius(p), $"{s}: {p.Kind} at {p.X},{p.Z} is {d:0.0} cells from the first view");
        }
    }

    [Theory]
    [InlineData(Scene.Beach)]
    [InlineData(Scene.Pool)]
    [InlineData(Scene.House)]
    [InlineData(Scene.Mountain)]
    public void Tree_crowns_stay_out_of_structures(Scene s)
    {
        var g = SceneKit.Make(s, 7);
        foreach (var t in g.Props.Where(p => p.Kind == PropKind.Tree))
        {
            var (min, max) = PropRules.Crown(t, t.Y >= 0 ? t.Y : SceneKit.TopFree(g.World, t.X, t.Z));
            foreach (var st in g.Structures)
            {
                bool hit = min.X <= st.Max.X && max.X >= st.Min.X && min.Y <= st.Max.Y && max.Y >= st.Min.Y && min.Z <= st.Max.Z && max.Z >= st.Min.Z;
                Assert.False(hit, $"{s}: the tree at {t.X},{t.Z} grows into {st.Name} at {st.Min}");
            }
        }
    }
}
