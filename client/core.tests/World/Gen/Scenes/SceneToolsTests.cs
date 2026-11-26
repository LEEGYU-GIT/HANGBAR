using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen.SceneChecks;

public class SceneToolsTests
{
    const int N = SceneContext.Size;

    static SceneContext Flat()
    {
        var ctx = new SceneContext(Palette.EnsureContrast(WorldGeneratorTests.MeadowPalette), WorldGeneratorTests.Calm, WorldGeneratorTests.Photo, 7);
        Array.Fill(ctx.Surface, 57);
        return ctx;
    }

    [Fact]
    public void FindPath_climbs_steps_of_two_at_most()
    {
        var s = Enumerable.Repeat(57, N * N).ToArray();
        // a cliff of 6 across the map at z = 100, except a ramp of steps of 2 at x 40..43
        for (int z = 100; z < N; z++)
            for (int x = 0; x < N; x++) s[z * N + x] = 63;
        for (int x = 40; x < 44; x++) { s[100 * N + x] = 59; s[101 * N + x] = 61; }
        var path = SceneTools.FindPath(s, (100, 60), (100, 150));
        Assert.NotNull(path);
        for (int i = 1; i < path!.Count; i++)
            Assert.True(Math.Abs(s[path[i].Z * N + path[i].X] - s[path[i - 1].Z * N + path[i - 1].X]) <= 2);
        Assert.Contains(path, p => p.X is >= 40 and < 44 && p.Z == 100);
    }

    [Fact]
    public void FindPath_goes_round_blocked_cells()
    {
        var s = Enumerable.Repeat(57, N * N).ToArray();
        bool Wall(int x, int z) => z == 96 && x < 150;
        var path = SceneTools.FindPath(s, (50, 60), (50, 130), Wall);
        Assert.NotNull(path);
        Assert.DoesNotContain(path!, p => Wall(p.X, p.Z));
    }

    [Fact]
    public void FenceRing_leaves_its_gates_open()
    {
        var ctx = Flat();
        Strata.Fill(ctx);
        var f = new LocalFrame(0);
        SceneTools.FenceRing(ctx, f, 40, 40, 100, 90, 57, (1, 70, 6));
        // the gate on side 1 (b = 90) at a 67..72 is open; the rest of that side is fenced
        for (int a = 67; a < 73; a++) Assert.False(Block.IsSolid(ctx.World.Get(a, 59, 90)), $"gate blocked at {a}");
        Assert.True(Block.IsSolid(ctx.World.Get(50, 59, 90)));
        Assert.True(Block.IsSolid(ctx.World.Get(40, 59, 60)));   // side 2 (a = 40)
        Assert.True(Block.IsSolid(ctx.World.Get(100, 59, 60)));  // side 3 (a = 100)
        Assert.True(Block.IsSolid(ctx.World.Get(60, 59, 40)));   // side 0 (b = 40)
    }

    [Fact]
    public void Road_marks_sidewalks_as_path()
    {
        var ctx = Flat();
        var f = new LocalFrame(0);
        var road = Streets.Road(ctx, f, 0, N, 96, lanes: 2, sidewalk: 12);
        // carriageway 2 × 12 = 24 wide around b = 96, sidewalks 12 on each side, raised one block
        Assert.Equal((84, 108), (road.CarriageFrom, road.CarriageTo));
        Assert.False(ctx.Path[96 * N + 50]);
        Assert.True(ctx.Path[80 * N + 50] && ctx.Path[110 * N + 50]);
        Assert.Equal(ctx.Surface[96 * N + 50] + 1, ctx.Surface[80 * N + 50]);
        Assert.Equal(Material.Concrete, ctx.TopMaterial[90 * N + 50]);
        Assert.Contains(Enumerable.Range(0, N), a => ctx.TopMaterial[96 * N + a] == Material.Tile); // the centre line
    }

    [Fact]
    public void Interior_rooms_have_real_ceilings_and_doors()
    {
        var ctx = Flat();
        Strata.Fill(ctx);
        var f = new LocalFrame(0);
        var plan = new InteriorPlan(30, 20, Material.Brick,
            new[] { new InteriorRoom("hall", 1, 29, 1, 9, Material.Tile), new InteriorRoom("storage", 1, 29, 10, 19, Material.Concrete) },
            new[] { new InteriorDoor(false, 9, 12), new InteriorDoor(false, 0, 12) });
        var b = Interior.Build(ctx, f, 80, 80, plan);
        // standing on the floor inside, 11 free blocks up to the roof slab
        for (int up = 0; up < 11; up++) Assert.False(Block.IsSolid(ctx.World.Get(90, b.Floor + up, 85)));
        Assert.True(Block.IsSolid(ctx.World.Get(90, b.Floor + 11, 85)));
        // the outer door (front wall q = 0 → b = 80) is 4 wide and 9 high
        for (int a = 92; a < 96; a++)
            for (int y = b.Floor; y < b.Floor + 9; y++) Assert.False(Block.IsSolid(ctx.World.Get(a, y, 80)));
        Assert.True(Block.IsSolid(ctx.World.Get(91, b.Floor, 80)) && Block.IsSolid(ctx.World.Get(96, b.Floor, 80)));
        Assert.Contains(ctx.Structures, s => s.Name == "doorway");
        Assert.Contains(ctx.Structures, s => s.Name == "room_storage");
    }
}
