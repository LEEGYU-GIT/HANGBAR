using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class AlleySceneTests : SceneContract
{
    protected override Scene Kind => Scene.Alley;
    protected override string[] Landmarks => new[] { "old_tree_square", "well", "viewpoint" };

    [Fact]
    public void Low_houses_line_the_alleys() => Assert.True(World.Structures.Count(s => s.Name == "house_shell") >= 12);

    [Fact]
    public void The_top_terrace_is_reached_without_jumping()
    {
        // the hillside climbs in terraces of four blocks; the main alley's steps lift you without a jump
        var g = World;
        var reach = SceneKit.ReachWithoutJumping(g, g.Spawn);
        int top = reach.Max(p => p.Y);
        Assert.True(top - g.Spawn.Y >= 16, $"climbed only {top - g.Spawn.Y} blocks");
    }

    [Fact]
    public void Small_house_can_be_entered() =>
        Assert.Contains(World.Structures, s => s.Name == "room_back_room");
}
