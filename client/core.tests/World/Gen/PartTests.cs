using Hangbar.Core.World;
using Hangbar.Core.World.Gen;

namespace Hangbar.Core.Tests.World.Gen;

public class PartTests
{
    static readonly PaletteRoles Colors = new(0x808080, 0x606060, 0x30A040, 0x202020, 0xE0E0E0);

    const string Sample = """
        part: sample              # a test part
        size: 3 2 2               # width height depth
        legend:
          S = Stone primary
          G = Glass #C8E6F0       # fixed colour
          W = Wood dark
        anchor: door 2 0 1
        layer 0:
        SG_
        S.W
        layer 1:
        S__
        ___
        """;

    static VoxelWorld Filled(uint b)
    {
        var w = new VoxelWorld(16, 16, 16);
        for (int z = 0; z < 16; z++)
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++) w.Set(x, y, z, b);
        return w;
    }

    static readonly uint Dirt = Block.Solid(0x553311, Material.Soil);

    [Fact]
    public void Parse_reads_size_legend_and_layers()
    {
        var p = Part.Parse(Sample, "sample");
        Assert.Equal("sample", p.Name);
        Assert.Equal((3, 2, 2), (p.Width, p.Height, p.Depth));
        Assert.Equal(new PartCell(Material.Stone, ColorRole.Primary, 0), p.CellAt(0, 0, 0));
        Assert.Equal(new PartCell(Material.Glass, null, 0xC8E6F0), p.CellAt(1, 0, 0));
        Assert.Null(p.CellAt(2, 0, 0));                                    // "_" keeps the world
        Assert.Equal(Material.Air, p.CellAt(1, 0, 1)!.Value.Material);      // "." carves air
        Assert.Equal(new PartCell(Material.Wood, ColorRole.Dark, 0), p.CellAt(2, 0, 1));
        Assert.Equal(new BlockPos(2, 0, 1), p.Anchors["door"]);
        Assert.Null(p.Stretch);
    }

    [Fact]
    public void Bad_part_reports_its_line()
    {
        string bad = Sample.Replace("S.W", "S.WW");
        var e = Assert.Throws<FormatException>(() => Part.Parse(bad, "broken"));
        Assert.Contains("broken", e.Message);
        Assert.Contains("line 10", e.Message);
    }

    [Fact]
    public void Roles_take_palette_colours()
    {
        var w = new VoxelWorld(16, 16, 16);
        PartPlacer.Place(w, Part.Parse(Sample, "sample"), new BlockPos(4, 2, 4), Rotation.R0, Colors, new PlaceOptions(), new Rng(1));
        Assert.Equal(Block.Solid(0x808080, Material.Stone), w.Get(4, 2, 4));
        Assert.Equal(Block.Solid(0x202020, Material.Wood), w.Get(6, 2, 5));
    }

    [Fact]
    public void Fixed_colour_is_kept()
    {
        var w = new VoxelWorld(16, 16, 16);
        PartPlacer.Place(w, Part.Parse(Sample, "sample"), new BlockPos(4, 2, 4), Rotation.R0, Colors, new PlaceOptions(), new Rng(1));
        Assert.Equal(Block.Solid(0xC8E6F0, Material.Glass), w.Get(5, 2, 4));
    }

    [Fact]
    public void Keep_cells_leave_the_world_alone()
    {
        var w = Filled(Dirt);
        PartPlacer.Place(w, Part.Parse(Sample, "sample"), new BlockPos(4, 2, 4), Rotation.R0, Colors, new PlaceOptions(), new Rng(1));
        Assert.Equal(Dirt, w.Get(6, 2, 4));     // "_"
        Assert.Equal(Block.Air, w.Get(5, 2, 5)); // "."
    }

    [Fact]
    public void Rotation_turns_the_part_clockwise()
    {
        // 3 wide (x) × 2 deep (z). Clockwise seen from above: east (+x) turns to south (+z), south turns to west.
        const string arrow = """
            size: 3 1 2
            legend:
              A = Stone primary
              B = Brick primary
            layer 0:
            ..A
            B..
            """;
        var w = new VoxelWorld(16, 16, 16);
        var placed = PartPlacer.Place(w, Part.Parse(arrow, "arrow"), new BlockPos(4, 0, 4), Rotation.R90, Colors, new PlaceOptions(), new Rng(1));
        Assert.Equal(Material.Stone, Block.MaterialOf(w.Get(4 + 1, 0, 4 + 2))); // (2,0) → (D-1-0, 2)
        Assert.Equal(Material.Brick, Block.MaterialOf(w.Get(4 + 0, 0, 4 + 0))); // (0,1) → (D-1-1, 0)
        Assert.Equal(new BlockPos(4, 0, 4), placed.Min);
        Assert.Equal(new BlockPos(5, 0, 6), placed.Max);
    }

    [Fact]
    public void Anchors_follow_rotation()
    {
        var p = Part.Parse(Sample, "sample"); // anchor door (2,0,1), width 3, depth 2
        var w = new VoxelWorld(16, 16, 16);
        Assert.Equal(new BlockPos(4 + 2, 2, 4 + 1), PartPlacer.Place(w, p, new BlockPos(4, 2, 4), Rotation.R0, Colors, new PlaceOptions(), new Rng(1)).Anchors["door"]);
        Assert.Equal(new BlockPos(4 + 0, 2, 4 + 2), PartPlacer.Place(w, p, new BlockPos(4, 2, 4), Rotation.R90, Colors, new PlaceOptions(), new Rng(1)).Anchors["door"]);
        Assert.Equal(new BlockPos(4 + 0, 2, 4 + 0), PartPlacer.Place(w, p, new BlockPos(4, 2, 4), Rotation.R180, Colors, new PlaceOptions(), new Rng(1)).Anchors["door"]);
        Assert.Equal(new BlockPos(4 + 1, 2, 4 + 0), PartPlacer.Place(w, p, new BlockPos(4, 2, 4), Rotation.R270, Colors, new PlaceOptions(), new Rng(1)).Anchors["door"]);
    }

    [Fact]
    public void Stretch_repeats_the_marked_columns()
    {
        const string bar = """
            size: 4 1 1
            legend:
              L = Stone primary
              M = Metal primary
              R = Brick primary
            stretch: x 1 2
            layer 0:
            LMMR
            """;
        var w = new VoxelWorld(16, 4, 4);
        var placed = PartPlacer.Place(w, Part.Parse(bar, "bar"), new BlockPos(2, 0, 1), Rotation.R0, Colors, new PlaceOptions(StretchTo: 10), new Rng(1));
        Assert.Equal(10, placed.Max.X - placed.Min.X + 1);
        Assert.Equal(Material.Stone, Block.MaterialOf(w.Get(2, 0, 1)));
        Assert.Equal(Material.Brick, Block.MaterialOf(w.Get(11, 0, 1)));
        for (int x = 3; x <= 10; x++) Assert.Equal(Material.Metal, Block.MaterialOf(w.Get(x, 0, 1)));
    }

    [Fact]
    public void Missing_drops_about_that_share()
    {
        string slab = "size: 40 1 40\nlegend:\n  S = Stone primary\nlayer 0:\n" + string.Join("\n", Enumerable.Repeat(new string('S', 40), 40));
        var w = new VoxelWorld(48, 4, 48);
        PartPlacer.Place(w, Part.Parse(slab, "slab"), new BlockPos(0, 0, 0), Rotation.R0, Colors, new PlaceOptions(Missing: 0.2), new Rng(3));
        int present = 0;
        for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
                if (Block.IsSolid(w.Get(x, 0, z))) present++;
        Assert.InRange(1 - present / 1600.0, 0.15, 0.25);
    }

    [Fact]
    public void State_is_applied_to_placed_blocks()
    {
        var w = new VoxelWorld(16, 16, 16);
        PartPlacer.Place(w, Part.Parse(Sample, "sample"), new BlockPos(4, 2, 4), Rotation.R0, Colors, new PlaceOptions(State: BlockState.Cracked), new Rng(1));
        Assert.Equal(BlockState.Cracked, Block.StateOf(w.Get(4, 2, 4)));
    }

    [Fact]
    public void Library_loads_every_embedded_part()
    {
        Assert.Contains("stone_steps", PartLibrary.Names);
        Assert.Contains("fence_segment", PartLibrary.Names);
        foreach (var name in PartLibrary.Names)
        {
            var p = PartLibrary.Get(name);
            Assert.True(p.Width > 0 && p.Height > 0 && p.Depth > 0, name);
        }
        Assert.Throws<KeyNotFoundException>(() => PartLibrary.Get("no_such_part"));
    }

    [Fact]
    public void Palette_roles_come_from_the_palette()
    {
        var r = PaletteRoles.From(new uint[] { 0x101010, 0x404040, 0x30B050, 0xA0A0A0, 0xF0F0F0 });
        Assert.Equal(0x30B050u, r.Accent);                     // most saturated mid colour
        Assert.True(Palette.Luma(r.Dark) < Palette.Luma(r.Secondary));
        Assert.True(Palette.Luma(r.Secondary) < Palette.Luma(r.Light));
        Assert.Equal(r.Primary, r[ColorRole.Primary]);
        var one = PaletteRoles.From(new uint[] { 0x777777 });  // a one-colour palette still works
        Assert.Equal(0x777777u, one.Accent);
    }
}
