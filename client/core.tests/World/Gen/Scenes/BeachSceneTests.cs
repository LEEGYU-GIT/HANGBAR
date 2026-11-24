using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class BeachSceneTests
{
    const int N = SceneKit.N;
    static readonly Lazy<GeneratedWorld> World = new(() => SceneKit.Make(Scene.Beach, 7));

    /// <summary>Per column: distance (4-steps) to the nearest column of the other kind (land↔water); -1 where unknown.</summary>
    static int[] DistanceToOther(FileMap m, bool fromWater)
    {
        var d = Enumerable.Repeat(-1, N * N).ToArray();
        var q = new Queue<int>();
        for (int i = 0; i < N * N; i++)
            if (m.Water[i] != fromWater) { d[i] = 0; q.Enqueue(i); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % N, z = i / N;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= N || nz >= N) continue;
                int j = nz * N + nx;
                if (d[j] >= 0) continue;
                d[j] = d[i] + 1;
                q.Enqueue(j);
            }
        }
        return d;
    }

    [Fact]
    public void Sea_covers_30_to_60_percent() =>
        Assert.InRange(World.Value.Surface.Water.Count(w => w) / (double)(N * N), 0.30, 0.60);

    [Fact]
    public void Shore_is_sand()
    {
        var g = World.Value;
        var toWater = DistanceToOther(g.Surface, fromWater: false); // for land columns: distance to water
        int land = 0, sand = 0;
        for (int i = 0; i < N * N; i++)
        {
            if (g.Surface.Water[i] || toWater[i] < 1 || toWater[i] > 12) continue;
            land++;
            int x = i % N, z = i / N, top = SceneKit.TopFree(g.World, x, z) - 1;
            if (Block.MaterialOf(g.World.Get(x, top, z)) == Material.Sand) sand++;
        }
        Assert.True(sand >= 0.8 * land, $"{sand}/{land}");
    }

    [Fact]
    public void Sea_floor_deepens_away_from_shore()
    {
        var m = World.Value.Surface;
        var toLand = DistanceToOther(m, fromWater: true);
        double Depth(int dist) => Enumerable.Range(0, N * N).Where(i => m.Water[i] && toLand[i] == dist)
            .Select(i => SceneContext.WaterLevel - m.Height[i]).DefaultIfEmpty(0).Average();
        Assert.True(Depth(10) < Depth(40), $"{Depth(10)} vs {Depth(40)}");
    }

    [Fact]
    public void Spawn_stands_on_sand_facing_the_sea()
    {
        var g = World.Value;
        Assert.Equal(Material.Sand, Block.MaterialOf(g.World.Get(g.Spawn.X, g.Spawn.Y - 1, g.Spawn.Z)));
        // looking straight ahead 40 cells: the view crosses the shore and ends over water
        double yaw = g.FacingDegrees * Math.PI / 180;
        int tx = g.Spawn.X + (int)Math.Round(-Math.Sin(yaw) * 60), tz = g.Spawn.Z + (int)Math.Round(-Math.Cos(yaw) * 60);
        Assert.True(g.Surface.Water[g.Surface.Index(tx, tz)], "the first view does not look at the sea");
        Assert.True(SceneKit.SeesFromSpawn(g, (tx + 0.5, SceneContext.WaterLevel + 0.5, tz + 0.5)));
    }

    [Fact]
    public void Pier_reaches_into_the_water()
    {
        var g = World.Value;
        var pier = g.Structures.First(s => s.Name == "pier_deck");
        int over = 0;
        for (int z = pier.Min.Z; z <= pier.Max.Z; z++)
            for (int x = pier.Min.X; x <= pier.Max.X; x++)
                if (Block.IsWater(g.World.Get(x, SceneContext.WaterLevel - 1, z))) over++;
        Assert.True(over >= 40, $"{over} deck columns over water");
    }

    [Fact]
    public void One_hut_can_be_entered()
    {
        var g = World.Value;
        var reach = Walkability.ReachCells(g.World, g.Spawn);
        Assert.Contains(g.Structures.Where(s => s.Name == "beach_hut"), hut =>
            reach.Any(p => p.X > hut.Min.X && p.X < hut.Max.X && p.Z > hut.Min.Z && p.Z < hut.Max.Z && p.Y <= hut.Min.Y + 2));
    }

    [Fact]
    public void Most_of_the_map_is_walkable() => Assert.True(SceneKit.WalkableShare(World.Value) >= 0.7);

    [Fact]
    public void Landmark_is_one_of_three()
    {
        var seen = new HashSet<string>();
        for (ulong s = 1; s <= 12; s++) seen.Add(SceneKit.Make(Scene.Beach, s).Landmark.Name);
        Assert.True(seen.IsSubsetOf(new[] { "shipwreck", "sea_arch", "lighthouse" }), string.Join(",", seen));
        Assert.True(seen.Count >= 2);
    }

    [Fact]
    public void Two_seeds_differ() => Assert.True(SceneKit.SurfaceDifference(World.Value, SceneKit.Make(Scene.Beach, 8)) >= 0.3);

    [Fact]
    public void Mural_rules_hold_for_ten_seeds() => SceneKit.MuralRulesHold(Scene.Beach);

    [Fact]
    public void Props_stand_on_something() => SceneKit.PropsStandOnSomething(World.Value);

    [Fact]
    public void Triangles_stay_under_budget()
    {
        int tris = SceneKit.Triangles(World.Value.World);
        Assert.True(tris <= SceneKit.TriangleBudget, $"{tris} triangles");
    }
}
