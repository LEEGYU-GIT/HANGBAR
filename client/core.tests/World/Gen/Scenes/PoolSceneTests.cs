using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class PoolSceneTests
{
    const int N = SceneKit.N, Deck = 56;
    static readonly Lazy<GeneratedWorld> World = new(() => SceneKit.Make(Scene.Pool, 7));

    /// <summary>Water cells of the open pool (columns whose top is water; cave water underground is left out).</summary>
    static List<(int X, int Y, int Z)> PoolWater(GeneratedWorld g)
    {
        var cells = new List<(int, int, int)>();
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                if (!g.Surface.Water[g.Surface.Index(x, z)]) continue;
                for (int y = 40; y <= Deck + 2; y++)
                    if (Block.IsWater(g.World.Get(x, y, z))) cells.Add((x, y, z));
            }
        return cells;
    }

    /// <summary>The pool's water columns as a box: min/max x and z.</summary>
    static (int X0, int Z0, int X1, int Z1) PoolBox(GeneratedWorld g)
    {
        int x0 = N, z0 = N, x1 = -1, z1 = -1;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                if (g.Surface.Water[g.Surface.Index(x, z)])
                {
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                    z0 = Math.Min(z0, z); z1 = Math.Max(z1, z);
                }
        return (x0, z0, x1, z1);
    }

    [Fact]
    public void Pool_basin_is_tiled()
    {
        var g = World.Value;
        var water = PoolWater(g);
        int solid = 0, tile = 0;
        var seen = new HashSet<(int, int, int)>();
        foreach (var (x, y, z) in water)
            foreach (var (dx, dy, dz) in new[] { (0, -1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1) })
            {
                var p = (x + dx, y + dy, z + dz);
                uint b = g.World.Get(p.Item1, p.Item2, p.Item3);
                if (!Block.IsSolid(b) || !seen.Add(p)) continue;
                solid++;
                if (Block.MaterialOf(b) == Material.Tile) tile++;
            }
        Assert.True(solid > 1000, $"{solid} basin blocks");
        Assert.True(tile >= 0.9 * solid, $"{tile}/{solid} tile");
    }

    [Fact]
    public void Water_is_5_to_8_blocks_deep()
    {
        var g = World.Value;
        var depth = PoolWater(g).GroupBy(c => (c.X, c.Z)).Select(c => c.Count()).ToList();
        Assert.True(depth.Count >= 64 * 32, $"{depth.Count} pool columns");
        Assert.All(depth, d => Assert.True(d <= 8, $"{d} deep"));
        Assert.True(depth.Count(d => d >= 5) >= 0.95 * depth.Count, "the pool is too shallow"); // the exit steps are shallower
        Assert.Contains(5, depth);
        Assert.Contains(8, depth);
    }

    [Fact]
    public void Lane_floats_run_the_length_of_the_pool()
    {
        var g = World.Value;
        var (x0, z0, x1, z1) = PoolBox(g);
        bool alongX = x1 - x0 > z1 - z0;
        int length = alongX ? x1 - x0 + 1 : z1 - z0 + 1;
        var lines = g.Props.Where(p => p.Kind == PropKind.Float)
            .GroupBy(p => alongX ? p.Z : p.X)
            .Where(line => (alongX ? line.Max(p => p.X) - line.Min(p => p.X) : line.Max(p => p.Z) - line.Min(p => p.Z)) + 1 >= 0.8 * length)
            .ToList();
        Assert.True(lines.Count >= 2, $"{lines.Count} float lines");
        Assert.All(lines.SelectMany(l => l), p => Assert.Equal(Deck + 1, p.Y)); // on the water surface
    }

    [Fact]
    public void You_can_walk_out_of_the_pool()
    {
        var g = World.Value;
        var deep = PoolWater(g).GroupBy(c => (c.X, c.Z)).First(c => c.Count() == 8);
        var floor = new BlockPos(deep.Key.X, deep.Min(c => c.Y), deep.Key.Z);
        Assert.Contains(g.Spawn, Walkability.ReachCells(g.World, floor));
    }

    [Fact]
    public void Ladders_stand_at_the_edge()
    {
        var g = World.Value;
        var ladders = g.Structures.Where(s => s.Name == "pool_ladder").ToList();
        Assert.True(ladders.Count >= 4, $"{ladders.Count} ladders");
        foreach (var l in ladders)
        {
            bool water = false, deck = false;
            for (int y = l.Min.Y; y <= l.Max.Y; y++)
                for (int z = l.Min.Z; z <= l.Max.Z; z++)
                    for (int x = l.Min.X; x <= l.Max.X; x++)
                    {
                        uint b = g.World.Get(x, y, z);
                        water |= Block.IsWater(b);
                        deck |= y == Deck && Block.MaterialOf(b) == Material.Tile;
                    }
            Assert.True(water && deck, $"ladder at {l.Min} is not on the pool's edge");
        }
    }

    [Fact]
    public void Spawn_faces_down_the_pool()
    {
        var g = World.Value;
        var (x0, z0, x1, z1) = PoolBox(g);
        bool alongX = x1 - x0 > z1 - z0;
        double cx = (x0 + x1) / 2.0, cz = (z0 + z1) / 2.0;
        // the far end's middle, at the water surface
        double fx = alongX ? (Math.Abs(g.Spawn.X - x0) > Math.Abs(g.Spawn.X - x1) ? x0 : x1) : cx;
        double fz = alongX ? cz : (Math.Abs(g.Spawn.Z - z0) > Math.Abs(g.Spawn.Z - z1) ? z0 : z1);
        double axis = alongX ? (fx < g.Spawn.X ? 90 : -90) : (fz < g.Spawn.Z ? 0 : 180);
        Assert.True(SceneKit.AngleBetween(g.FacingDegrees, axis) <= 10, $"facing {g.FacingDegrees}, pool axis {axis}");
        Assert.True(SceneKit.SeesFromSpawn(g, (fx + 0.5, Deck + 1.5, fz + 0.5)), "the far end of the pool is hidden");
    }

    [Fact]
    public void Mural_hangs_framed_in_the_plant_room()
    {
        var g = World.Value;
        var room = g.Structures.Single(s => s.Name == "changing_room");
        var m = g.Mural.Origin;
        Assert.True(g.Mural.Framed);
        Assert.True(m.X >= room.Min.X && m.X <= room.Max.X && m.Z >= room.Min.Z && m.Z <= room.Max.Z && m.Y > room.Min.Y && m.Y < room.Max.Y,
            $"mural at {m} is not in the changing room {room.Min}..{room.Max}");
        Assert.True(HiddenMural.IsHidden(g.World, g.Spawn, g.Mural));
        Assert.True(HiddenMural.Distance(g.Spawn, g.Mural) >= HiddenMural.MinDistance);
        Assert.True(HiddenMural.IsReachable(Walkability.ReachCells(g.World, g.Spawn), g.Mural));
    }

    [Fact]
    public void Most_of_the_map_is_walkable() => Assert.True(SceneKit.WalkableShare(World.Value) >= 0.7);

    [Fact]
    public void Landmark_is_one_of_two()
    {
        var seen = new HashSet<string>();
        for (ulong s = 1; s <= 12; s++) seen.Add(SceneKit.Make(Scene.Pool, s).Landmark.Name);
        Assert.True(seen.SetEquals(new[] { "water_slide", "high_dive_tower" }), string.Join(",", seen));
    }

    [Fact]
    public void Two_seeds_differ()
    {
        var a = World.Value;
        var b = SceneKit.Make(Scene.Pool, 8);
        Assert.True(PoolBox(a) != PoolBox(b) || a.Landmark.Name != b.Landmark.Name, "same pool, same landmark");
        var spots = b.Props.Select(p => (p.X, p.Z)).ToHashSet();
        double moved = a.Props.Count(p => !spots.Contains((p.X, p.Z))) / (double)a.Props.Count;
        Assert.True(moved >= 0.3, $"only {moved:P0} of the props moved");
    }

    [Fact]
    public void Mural_rules_hold_for_ten_seeds() => SceneKit.MuralRulesHold(Scene.Pool);

    [Fact]
    public void Props_stand_on_something() => SceneKit.PropsStandOnSomething(World.Value);

    [Fact]
    public void Triangles_stay_under_budget()
    {
        int tris = SceneKit.Triangles(World.Value.World);
        Assert.True(tris <= SceneKit.TriangleBudget, $"{tris} triangles");
    }
}
