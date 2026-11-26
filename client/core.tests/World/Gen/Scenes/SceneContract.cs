using System.Collections.Concurrent;
using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

/// <summary>
/// The checks every part-2 scene keeps (v2 part 2 plan, "공통 테스트"). A scene's test class inherits this and adds
/// its own facts. Seed 7's world is generated once per scene and shared.
/// </summary>
public abstract class SceneContract
{
    static readonly ConcurrentDictionary<Scene, Lazy<GeneratedWorld>> Worlds = new();

    protected abstract Scene Kind { get; }
    /// <summary>The landmark names this scene may choose.</summary>
    protected abstract string[] Landmarks { get; }

    protected GeneratedWorld World => Worlds.GetOrAdd(Kind, k => new Lazy<GeneratedWorld>(() => SceneKit.Make(k, 7))).Value;
    protected const int N = SceneKit.N;

    [Fact]
    public void Most_of_the_map_is_walkable() => Assert.True(SceneKit.WalkableShare(World) >= 0.7, $"{SceneKit.WalkableShare(World):P0}");

    [Fact]
    public void Spawn_is_free_and_sees_its_target()
    {
        var g = World;
        Assert.True(Walkability.IsStandable(g.World, g.Spawn.X, g.Spawn.Y, g.Spawn.Z), "the spawn is blocked");
        Assert.True(SceneKit.SeesFromSpawn(g, (g.Target.X + 0.5, g.Target.Y + 0.5, g.Target.Z + 0.5)), "the first view is blocked");
    }

    [Fact]
    public void Ten_seeds_keep_the_mural_rules_and_vary_the_landmark()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var g = SceneKit.Make(Kind, seed);
            seen.Add(g.Landmark.Name);
            Assert.True(HiddenMural.IsHidden(g.World, g.Spawn, g.Mural), $"seed {seed}: mural visible from the spawn");
            Assert.True(HiddenMural.Distance(g.Spawn, g.Mural) >= HiddenMural.MinDistance, $"seed {seed}: mural too close");
            Assert.True(HiddenMural.IsReachable(Walkability.ReachCells(g.World, g.Spawn), g.Mural), $"seed {seed}: mural out of reach");
        }
        Assert.True(seen.IsSubsetOf(Landmarks), string.Join(",", seen));
        Assert.True(seen.Count >= 2, $"only {string.Join(",", seen)}");
    }

    [Fact]
    public void Two_seeds_differ()
    {
        var a = World;
        var b = SceneKit.Make(Kind, 8);
        double ground = SceneKit.SurfaceDifference(a, b);
        var spots = b.Structures.Select(s => s.Min).ToHashSet();
        double moved = a.Structures.Count == 0 ? 1 : a.Structures.Count(s => !spots.Contains(s.Min)) / (double)a.Structures.Count;
        Assert.True(ground >= 0.3 || moved >= 0.3, $"ground {ground:P0}, structures {moved:P0}");
    }

    [Fact]
    public void Props_stand_on_something() => SceneKit.PropsStandOnSomething(World);

    [Fact]
    public void Triangles_stay_under_budget()
    {
        int tris = SceneKit.Triangles(World.World);
        Assert.True(tris <= SceneKit.TriangleBudget, $"{tris} triangles");
    }

    [Fact]
    public void Doors_need_no_jump() => SceneKit.DoorsNeedNoJump(World);
}
