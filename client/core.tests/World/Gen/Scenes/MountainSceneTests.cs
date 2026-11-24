using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class MountainSceneTests
{
    const int N = SceneKit.N;
    static readonly Lazy<GeneratedWorld> World = new(() => SceneKit.Make(Scene.Mountain, 7));

    /// <summary>Highest terrain column (structures excluded) and its height.</summary>
    static (int X, int Z, int Top) Summit(GeneratedWorld g)
    {
        bool InStructure(int x, int z) => g.Structures.Any(s => x >= s.Min.X && x <= s.Max.X && z >= s.Min.Z && z <= s.Max.Z);
        (int, int, int) best = (0, 0, 0);
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int h = g.Surface.Height[g.Surface.Index(x, z)];
                if (h > best.Item3 && !InStructure(x, z)) best = (x, z, h);
            }
        return best;
    }

    [Fact]
    public void Peak_is_15_to_20_metres_over_the_ground() => Assert.InRange(Summit(World.Value).Top, 116, 136);

    [Fact]
    public void There_are_cliffs()
    {
        var m = World.Value.Surface;
        int pairs = 0;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                if (x + 1 < N && Math.Abs(m.Height[m.Index(x, z)] - m.Height[m.Index(x + 1, z)]) >= 3) pairs++;
                if (z + 1 < N && Math.Abs(m.Height[m.Index(x, z)] - m.Height[m.Index(x, z + 1)]) >= 3) pairs++;
            }
        Assert.True(pairs >= 200, $"{pairs} cliff pairs");
    }

    [Fact]
    public void Trail_reaches_the_summit()
    {
        var g = World.Value;
        var (sx, sz, top) = Summit(g);
        var reach = Walkability.ReachCells(g.World, g.Spawn);
        Assert.Contains(reach, p => Math.Max(Math.Abs(p.X - sx), Math.Abs(p.Z - sz)) <= 3 && p.Y >= top - 2);
    }

    [Fact]
    public void Most_of_the_map_is_walkable() => Assert.True(SceneKit.WalkableShare(World.Value) >= 0.7);

    [Fact]
    public void Spawn_faces_the_summit()
    {
        var g = World.Value;
        var (sx, sz, top) = Summit(g);
        Assert.True(SceneKit.AngleBetween(g.FacingDegrees, SceneKit.FacingTo(g.Spawn, sx, sz)) <= 15);
        Assert.True(SceneKit.SeesFromSpawn(g, (sx + 0.5, top + 0.5, sz + 0.5)));
    }

    [Fact]
    public void Hut_and_cairn_stand()
    {
        var names = World.Value.Structures.Select(s => s.Name).ToList();
        Assert.Contains("mountain_hut", names);
        Assert.Contains("summit_cairn", names);
    }

    [Fact]
    public void Hut_can_be_entered()
    {
        var g = World.Value;
        var hut = g.Structures.First(s => s.Name == "mountain_hut");
        var reach = Walkability.ReachCells(g.World, g.Spawn);
        Assert.Contains(reach, p => p.X > hut.Min.X && p.X < hut.Max.X && p.Z > hut.Min.Z && p.Z < hut.Max.Z && p.Y <= hut.Min.Y + 2);
    }

    [Fact]
    public void Landmark_is_one_of_three()
    {
        var seen = new HashSet<string>();
        for (ulong s = 1; s <= 12; s++) seen.Add(SceneKit.Make(Scene.Mountain, s).Landmark.Name);
        Assert.True(seen.IsSubsetOf(new[] { "ruined_tower", "stone_arch", "giant_tree" }), string.Join(",", seen));
        Assert.True(seen.Count >= 2, "seeds should pick different landmarks");
    }

    [Fact]
    public void Two_seeds_differ() => Assert.True(SceneKit.SurfaceDifference(World.Value, SceneKit.Make(Scene.Mountain, 8)) >= 0.3);

    [Fact]
    public void Mural_rules_hold_for_ten_seeds() => SceneKit.MuralRulesHold(Scene.Mountain);

    [Fact]
    public void Props_stand_on_something() => SceneKit.PropsStandOnSomething(World.Value);

    [Fact]
    public void Triangles_stay_under_budget()
    {
        int tris = SceneKit.Triangles(World.Value.World);
        Assert.True(tris <= SceneKit.TriangleBudget, $"{tris} triangles");
    }

}
