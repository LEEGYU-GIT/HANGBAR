using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class MeadowSceneTests : SceneContract
{
    protected override Scene Kind => Scene.Meadow;
    protected override string[] Landmarks => new[] { "windmill", "lone_tree", "stone_circle" };

    [Fact]
    public void Rolling_hills_stay_gentle()
    {
        var m = World.Surface;
        int gentle = 0, total = 0;
        for (int z = 1; z < N - 1; z++)
            for (int x = 1; x < N - 1; x++)
            {
                if (m.Water[m.Index(x, z)] || World.Structures.Any(s => x >= s.Min.X - 1 && x <= s.Max.X + 1 && z >= s.Min.Z - 1 && z <= s.Max.Z + 1)) continue;
                total++;
                int h = m.Height[m.Index(x, z)];
                if (Math.Abs(h - m.Height[m.Index(x + 1, z)]) <= 3 && Math.Abs(h - m.Height[m.Index(x, z + 1)]) <= 3) gentle++;
            }
        Assert.True(gentle >= 0.95 * total, $"{gentle}/{total}");
    }

    [Fact]
    public void Flowers_cover_the_grass() => Assert.True(World.Props.Count(p => p.Kind == PropKind.Plant) >= 60);

    [Fact]
    public void There_is_a_pond() => Assert.InRange(World.Surface.Water.Count(w => w), 150, 2500);

    [Fact]
    public void A_path_loops_round_the_meadow() =>
        Assert.True(World.Stairs.Count(s => s) >= 400, "the path is too short");
}
