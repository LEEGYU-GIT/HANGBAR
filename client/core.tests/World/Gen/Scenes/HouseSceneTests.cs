using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class HouseSceneTests
{
    const int Basement = 45, Ground = 57, Upstairs = 69, Rooftop = 81;
    static readonly int[] Floors = { Basement, Ground, Upstairs };
    static readonly Dictionary<string, int> Heights = new()
    {
        ["dining_table"] = 3, ["coffee_table"] = 3, ["chair"] = 2, ["bed"] = 2, ["bookshelf"] = 8, ["wardrobe"] = 8,
    };
    static readonly HashSet<string> Furniture = new()
    {
        "sofa", "tv_stand", "coffee_table", "kitchen_counter", "fridge", "dining_table", "chair", "bed", "wardrobe", "bathtub", "sink", "bookshelf",
    };
    static readonly Lazy<GeneratedWorld> World = new(() => SceneKit.Make(Scene.House, 7));

    static PlacedPart House(GeneratedWorld g) => g.Structures.Single(s => s.Name == "house");

    static bool InBox(PlacedPart b, int x, int y, int z) =>
        x >= b.Min.X && x <= b.Max.X && y >= b.Min.Y && y <= b.Max.Y && z >= b.Min.Z && z <= b.Max.Z;

    /// <summary>Cells someone can stand in on the three floors inside the house.</summary>
    static List<BlockPos> IndoorFloor(GeneratedWorld g)
    {
        var h = House(g);
        var cells = new List<BlockPos>();
        for (int z = h.Min.Z; z <= h.Max.Z; z++)
            for (int x = h.Min.X; x <= h.Max.X; x++)
                foreach (int y in Floors)
                    if (Walkability.IsStandable(g.World, x, y, z)) cells.Add(new BlockPos(x, y, z));
        return cells;
    }

    [Fact]
    public void Rooms_have_real_ceilings()
    {
        var g = World.Value;
        var cells = IndoorFloor(g);
        int real = cells.Count(p =>
        {
            int up = 1;
            while (up <= 30 && !Block.IsSolid(g.World.Get(p.X, p.Y + up, p.Z))) up++;
            return up is >= 10 and <= 12;
        });
        Assert.True(cells.Count >= 1500, $"{cells.Count} floor cells");
        Assert.True(real >= 0.95 * cells.Count, $"{real}/{cells.Count} under a 10-12 block ceiling");
    }

    [Fact]
    public void Doors_are_4_by_9()
    {
        var g = World.Value;
        var doors = g.Structures.Where(s => s.Name == "doorway").ToList();
        Assert.True(doors.Count >= 8, $"{doors.Count} doors");
        foreach (var d in doors)
        {
            int dx = d.Max.X - d.Min.X + 1, dz = d.Max.Z - d.Min.Z + 1, dy = d.Max.Y - d.Min.Y + 1;
            Assert.True((dx, dz) is (4, 1) or (1, 4), $"door at {d.Min} is {dx}x{dz}");
            Assert.Equal(9, dy);
            for (int y = d.Min.Y; y <= d.Max.Y; y++)
                for (int z = d.Min.Z; z <= d.Max.Z; z++)
                    for (int x = d.Min.X; x <= d.Max.X; x++)
                        Assert.False(Block.IsSolid(g.World.Get(x, y, z)), $"door at {d.Min} is blocked at {x},{y},{z}");
            for (int z = d.Min.Z; z <= d.Max.Z; z++)
                for (int x = d.Min.X; x <= d.Max.X; x++)
                    Assert.True(Block.IsSolid(g.World.Get(x, d.Max.Y + 1, z)), $"door at {d.Min} has no lintel");
            var (ax, az, bx, bz) = dx == 4 ? (d.Min.X - 1, d.Min.Z, d.Max.X + 1, d.Min.Z) : (d.Min.X, d.Min.Z - 1, d.Min.X, d.Max.Z + 1);
            Assert.True(Block.IsSolid(g.World.Get(ax, d.Min.Y, az)) && Block.IsSolid(g.World.Get(bx, d.Min.Y, bz)), $"door at {d.Min} is wider than 4");
        }
    }

    [Fact]
    public void Outer_windows_are_glass()
    {
        var g = World.Value;
        var h = House(g);
        var doors = g.Structures.Where(s => s.Name == "doorway").ToList();
        int glass = 0;
        for (int z = h.Min.Z; z <= h.Max.Z; z++)
            for (int x = h.Min.X; x <= h.Max.X; x++)
            {
                if (x != h.Min.X && x != h.Max.X && z != h.Min.Z && z != h.Max.Z) continue;
                foreach (int s in new[] { Ground, Upstairs })
                    for (int y = s; y <= s + 10; y++)
                    {
                        uint b = g.World.Get(x, y, z);
                        if (Block.MaterialOf(b) == Material.Glass)
                        {
                            glass++;
                            Assert.InRange(y - s, 4, 8);
                        }
                        else if (!Block.IsSolid(b))
                            Assert.True(doors.Any(d => InBox(d, x, y, z)), $"hole in the outer wall at {x},{y},{z}");
                    }
            }
        Assert.True(glass >= 16, $"{glass} glass blocks in the outer walls");
    }

    [Fact]
    public void Every_room_can_be_reached()
    {
        var g = World.Value;
        var reach = Walkability.ReachCells(g.World, g.Spawn);
        var cells = IndoorFloor(g);
        int reached = cells.Count(reach.Contains);
        Assert.True(reached >= 0.95 * cells.Count, $"{reached}/{cells.Count} indoor floor cells reached");
    }

    [Fact]
    public void Upstairs_rooftop_and_basement_can_be_reached()
    {
        var g = World.Value;
        var h = House(g);
        var reach = Walkability.ReachCells(g.World, g.Spawn).Where(p => p.X >= h.Min.X && p.X <= h.Max.X && p.Z >= h.Min.Z && p.Z <= h.Max.Z).ToList();
        Assert.Contains(reach, p => p.Y == Upstairs);
        Assert.Contains(reach, p => p.Y == Rooftop);
        Assert.Contains(reach, p => p.Y == Basement);
    }

    [Fact]
    public void Every_floor_can_be_reached_without_jumping()
    {
        // only stairs lift you without a jump (user decision 2026-11-25)
        var g = World.Value;
        var h = House(g);
        var reach = SceneKit.ReachWithoutJumping(g, g.Spawn).Where(p => p.X >= h.Min.X && p.X <= h.Max.X && p.Z >= h.Min.Z && p.Z <= h.Max.Z).ToList();
        Assert.Contains(reach, p => p.Y == Upstairs);
        Assert.Contains(reach, p => p.Y == Rooftop);
        Assert.Contains(reach, p => p.Y == Basement);
    }

    [Fact]
    public void Doors_need_no_jump() => SceneKit.DoorsNeedNoJump(World.Value);

    [Fact]
    public void Spawn_is_inside_facing_the_window()
    {
        var g = World.Value;
        var s = g.Spawn;
        Assert.True(Enumerable.Range(1, 12).Any(k => Block.IsSolid(g.World.Get(s.X, s.Y + k, s.Z))), "no ceiling over the spawn");
        double yaw = g.FacingDegrees * Math.PI / 180;
        int dx = (int)Math.Round(-Math.Sin(yaw)), dz = (int)Math.Round(-Math.Cos(yaw));
        int eye = s.Y + 6;
        for (int d = 1; d <= 48; d++)
        {
            uint b = g.World.Get(s.X + dx * d, eye, s.Z + dz * d);
            if (!Block.IsSolid(b)) continue;
            Assert.Equal(Material.Glass, Block.MaterialOf(b));
            Assert.True(SceneKit.SeesFromSpawn(g, (s.X + dx * d + 0.5, eye + 0.5, s.Z + dz * d + 0.5)), "the window is hidden");
            return;
        }
        Assert.Fail("nothing ahead of the spawn");
    }

    [Fact]
    public void Yard_is_open_to_the_sky()
    {
        var g = World.Value;
        int closed = 0;
        for (int z = 0; z < SceneKit.N; z++)
            for (int x = 0; x < SceneKit.N; x++)
            {
                if (g.Structures.Any(s => x >= s.Min.X && x <= s.Max.X && z >= s.Min.Z && z <= s.Max.Z)) continue;
                if (SceneKit.TopFree(g.World, x, z) > Ground) closed++;
            }
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Each_room_has_furniture()
    {
        var g = World.Value;
        var rooms = g.Structures.Where(s => s.Name.StartsWith("room_") && s.Name is not ("room_stair" or "room_hall" or "room_hidden")).ToList();
        Assert.True(rooms.Count >= 7, $"{rooms.Count} furnished rooms");
        var furniture = g.Structures.Where(s => Furniture.Contains(s.Name)).ToList();
        foreach (var r in rooms)
        {
            int inside = furniture.Count(f => InBox(r, f.Min.X, f.Min.Y, f.Min.Z) && InBox(r, f.Max.X, f.Min.Y, f.Max.Z));
            Assert.True(inside >= 2, $"{r.Name} at {r.Min} has {inside} pieces of furniture");
        }
    }

    [Fact]
    public void Furniture_follows_real_sizes()
    {
        var g = World.Value;
        foreach (var (name, height) in Heights)
        {
            var pieces = g.Structures.Where(s => s.Name == name).ToList();
            Assert.NotEmpty(pieces);
            Assert.All(pieces, p => Assert.Equal(height, p.Max.Y - p.Min.Y + 1));
        }
    }

    [Fact]
    public void Mural_hangs_framed_in_the_basement()
    {
        var g = World.Value;
        var room = g.Structures.Single(s => s.Name == "room_hidden");
        var m = g.Mural;
        Assert.True(m.Framed);
        Assert.InRange(m.Origin.Y, Basement, Basement + 10);
        Assert.True(m.Origin.X >= room.Min.X - 1 && m.Origin.X <= room.Max.X + 1 && m.Origin.Z >= room.Min.Z - 1 && m.Origin.Z <= room.Max.Z + 1,
            $"mural at {m.Origin} is not in the hidden room {room.Min}..{room.Max}");
    }

    [Fact]
    public void Most_of_the_map_is_walkable() => Assert.True(SceneKit.WalkableShare(World.Value) >= 0.7);

    [Fact]
    public void Landmark_is_one_of_two()
    {
        var seen = new HashSet<string>();
        for (ulong s = 1; s <= 12; s++) seen.Add(SceneKit.Make(Scene.House, s).Landmark.Name);
        Assert.True(seen.SetEquals(new[] { "treehouse", "greenhouse" }), string.Join(",", seen));
    }

    [Fact]
    public void Two_seeds_differ()
    {
        var a = World.Value.Structures.Where(s => Furniture.Contains(s.Name)).Select(s => s.Min).ToList();
        var b = SceneKit.Make(Scene.House, 8).Structures.Where(s => Furniture.Contains(s.Name)).Select(s => s.Min).ToHashSet();
        double moved = a.Count(p => !b.Contains(p)) / (double)a.Count;
        Assert.True(moved >= 0.3, $"only {moved:P0} of the furniture moved");
    }

    [Fact]
    public void Mural_rules_hold_for_ten_seeds() => SceneKit.MuralRulesHold(Scene.House);

    [Fact]
    public void Props_stand_on_something() => SceneKit.PropsStandOnSomething(World.Value);

    [Fact]
    public void Triangles_stay_under_budget()
    {
        int tris = SceneKit.Triangles(World.Value.World);
        Assert.True(tris <= SceneKit.TriangleBudget, $"{tris} triangles");
    }
}
