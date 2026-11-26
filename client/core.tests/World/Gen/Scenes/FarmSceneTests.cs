using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class FarmSceneTests : SceneContract
{
    protected override Scene Kind => Scene.Farm;
    protected override string[] Landmarks => new[] { "silo", "windmill", "scarecrows" };

    [Fact]
    public void Fields_have_furrows()
    {
        var g = World;
        var fields = g.Structures.Where(s => s.Name == "field").ToList();
        Assert.True(fields.Count >= 4, $"{fields.Count} fields");
        int soil = 0, total = 0;
        foreach (var f in fields)
            for (int z = f.Min.Z; z <= f.Max.Z; z++)
                for (int x = f.Min.X; x <= f.Max.X; x++)
                {
                    total++;
                    int top = SceneKit.TopFree(g.World, x, z) - 1;
                    if (Block.MaterialOf(g.World.Get(x, top, z)) == Material.Soil) soil++;
                }
        Assert.InRange(soil / (double)total, 0.4, 0.6);
    }

    [Fact]
    public void Crops_grow_in_rows() => Assert.True(World.Props.Count(p => p.Kind == PropKind.Plant) >= 300);

    [Fact]
    public void Barn_loft_is_reached_without_jumping()
    {
        var g = World;
        var barn = g.Structures.Single(s => s.Name == "barn");
        var reach = SceneKit.ReachWithoutJumping(g, g.Spawn);
        Assert.Contains(reach, p => p.X > barn.Min.X && p.X < barn.Max.X && p.Z > barn.Min.Z && p.Z < barn.Max.Z && p.Y == barn.Min.Y + 13);
    }
}
