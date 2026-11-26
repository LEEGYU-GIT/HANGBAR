using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class BoulevardSceneTests : SceneContract
{
    protected override Scene Kind => Scene.Boulevard;
    protected override string[] Landmarks => new[] { "clock_tower", "billboard", "monument_gate" };

    int TopIs(Material m)
    {
        int n = 0;
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int top = SceneKit.TopFree(World.World, x, z) - 1;
                if (Block.MaterialOf(World.World.Get(x, top, z)) == m) n++;
            }
        return n;
    }

    [Fact]
    public void Crosswalks_and_lane_lines_are_painted() => Assert.True(TopIs(Material.Tile) >= 600, $"{TopIs(Material.Tile)} painted cells");

    [Fact]
    public void Sidewalks_are_paths() => Assert.True(World.Stairs.Count(s => s) >= 4000, $"{World.Stairs.Count(s => s)} sidewalk cells");

    [Fact]
    public void Buildings_line_the_street() => Assert.True(World.Structures.Count(s => s.Name == "facade") >= 6, $"{World.Structures.Count(s => s.Name == "facade")} buildings");

    [Fact]
    public void Shop_can_be_entered_without_jumping()
    {
        var g = World;
        var shop = g.Structures.Single(s => s.Name == "room_shop");
        var reach = SceneKit.ReachWithoutJumping(g, g.Spawn);
        Assert.Contains(reach, p => p.X >= shop.Min.X && p.X <= shop.Max.X && p.Z >= shop.Min.Z && p.Z <= shop.Max.Z && p.Y == shop.Min.Y);
    }
}
