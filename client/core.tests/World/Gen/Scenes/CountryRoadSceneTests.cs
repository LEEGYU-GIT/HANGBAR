using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class CountryRoadSceneTests : SceneContract
{
    protected override Scene Kind => Scene.CountryRoad;
    protected override string[] Landmarks => new[] { "stone_bridge", "chapel", "signpost_tree" };

    static bool Asphalt(GeneratedWorld g, int x, int z)
    {
        int top = SceneKit.TopFree(g.World, x, z) - 1;
        return Block.MaterialOf(g.World.Get(x, top, z)) == Material.Concrete;
    }

    [Fact]
    public void Road_runs_edge_to_edge()
    {
        var g = World;
        bool Near(Func<int, (int, int)> at) => Enumerable.Range(0, N).Any(k => { var (x, z) = at(k); return Asphalt(g, x, z); });
        bool westEast = Near(k => (1, k)) && Near(k => (N - 2, k));
        bool northSouth = Near(k => (k, 1)) && Near(k => (k, N - 2));
        Assert.True(westEast || northSouth, "the road does not cross the map");
    }

    [Fact]
    public void Road_is_gentle()
    {
        var g = World;
        var m = g.Surface;
        int steep = 0, total = 0;
        for (int z = 0; z < N - 1; z++)
            for (int x = 0; x < N - 1; x++)
            {
                if (!Asphalt(g, x, z)) continue;
                foreach (var (nx, nz) in new[] { (x + 1, z), (x, z + 1) })
                {
                    if (!Asphalt(g, nx, nz)) continue;
                    total++;
                    if (Math.Abs(m.Height[m.Index(x, z)] - m.Height[m.Index(nx, nz)]) > 1) steep++;
                }
            }
        Assert.True(total > 2000, $"{total} road steps");
        Assert.True(steep <= total / 100, $"{steep}/{total} road steps over one block");
    }

    [Fact]
    public void Trees_line_the_road() => Assert.True(World.Props.Count(p => p.Kind == PropKind.Tree) >= 25);
}
