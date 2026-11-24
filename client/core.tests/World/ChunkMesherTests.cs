using Hangbar.Core.World;

namespace Hangbar.Core.Tests.World;

public class ChunkMesherTests
{
    static readonly uint White = Block.Solid(0xFFFFFF);

    static VoxelWorld World(int sx = 16, int sy = 16, int sz = 16, params (int X, int Y, int Z, uint B)[] blocks)
    {
        var w = new VoxelWorld(sx, sy, sz);
        foreach (var b in blocks) w.Set(b.X, b.Y, b.Z, b.B);
        return w;
    }

    /// <summary>Red channel (0..1) of every vertex at the given position with an upward normal.</summary>
    static List<float> TopVertexReds(MeshData m, float x, float y, float z)
    {
        var reds = new List<float>();
        for (int v = 0; v < m.VertexCount; v++)
        {
            bool at = Math.Abs(m.Positions[v * 3] - x) < 1e-4 && Math.Abs(m.Positions[v * 3 + 1] - y) < 1e-4 && Math.Abs(m.Positions[v * 3 + 2] - z) < 1e-4;
            if (at && m.Normals[v * 3 + 1] > 0.5f) reds.Add(m.Colors[v * 4]);
        }
        return reds;
    }

    [Fact]
    public void Single_block_has_six_faces()
    {
        var meshes = ChunkMesher.Build(World(blocks: (5, 5, 5, White)), 0, 0, 0);
        var (solid, water) = (meshes.Opaque, meshes.Water);
        Assert.Equal(24, solid.VertexCount);
        Assert.Equal(12, solid.TriangleCount);
        Assert.Equal(0, water.VertexCount);
    }

    [Fact]
    public void Touching_blocks_hide_shared_faces() =>
        Assert.Equal(40, ChunkMesher.Build(World(blocks: new[] { (5, 5, 5, White), (6, 5, 5, White) }), 0, 0, 0).Opaque.VertexCount);

    [Fact]
    public void Faces_against_neighbor_chunk_are_culled() =>
        Assert.Equal(20, ChunkMesher.Build(World(32, 16, 16, (15, 5, 5, White), (16, 5, 5, White)), 0, 0, 0).Opaque.VertexCount);

    [Fact]
    public void Fully_exposed_top_face_has_no_occlusion()
    {
        var m = ChunkMesher.Build(World(blocks: (5, 5, 5, White)), 0, 0, 0).Opaque;
        foreach (var (x, z) in new[] { (1.25f, 1.25f), (1.5f, 1.25f), (1.25f, 1.5f), (1.5f, 1.5f) })
            Assert.Equal(new[] { 1f }, TopVertexReds(m, x, 1.5f, z));
    }

    [Fact]
    public void Corner_between_two_walls_is_darkest()
    {
        var m = ChunkMesher.Build(World(blocks: new[] { (5, 5, 5, White), (6, 6, 5, White), (5, 6, 6, White) }), 0, 0, 0).Opaque;
        Assert.Equal(new[] { 0.5f }, TopVertexReds(m, 1.5f, 1.5f, 1.5f));
        Assert.Equal(new[] { 1f }, TopVertexReds(m, 1.25f, 1.5f, 1.25f)); // far corner untouched
    }

    [Fact]
    public void Water_makes_only_top_faces()
    {
        var built = ChunkMesher.Build(World(blocks: new[] { (5, 5, 5, White), (6, 5, 5, White), (5, 6, 5, Block.Water), (6, 6, 5, Block.Water) }), 0, 0, 0);
        var (solid, water) = (built.Opaque, built.Water);
        Assert.Equal(8, water.VertexCount);
        Assert.All(Enumerable.Range(0, water.VertexCount), v => Assert.Equal(1f, water.Normals[v * 3 + 1]));
        Assert.Equal(1.75f - 0.025f, water.Positions[1], 4);
        Assert.NotEmpty(TopVertexReds(solid, 1.25f, 1.5f, 1.25f)); // floor top under the water is still drawn
    }

    [Fact]
    public void Positions_are_in_meters()
    {
        var m = ChunkMesher.Build(World(blocks: (4, 0, 0, White)), 0, 0, 0).Opaque;
        for (int v = 0; v < m.VertexCount; v++) Assert.Contains(m.Positions[v * 3], new[] { 1.0f, 1.25f });
    }

    [Fact]
    public void Triangles_wind_clockwise_seen_from_outside()
    {
        // Godot treats clockwise triangles as front faces, so cross(b-a, c-a) must point against the face normal.
        var m = ChunkMesher.Build(World(blocks: (5, 5, 5, White)), 0, 0, 0).Opaque;
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = m.Indices[t * 3], b = m.Indices[t * 3 + 1], c = m.Indices[t * 3 + 2];
            float[] P(int i) => new[] { m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2] };
            var (pa, pb, pc) = (P(a), P(b), P(c));
            float[] e1 = { pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2] }, e2 = { pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2] };
            float[] cr = { e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0] };
            float dot = cr[0] * m.Normals[a * 3] + cr[1] * m.Normals[a * 3 + 1] + cr[2] * m.Normals[a * 3 + 2];
            Assert.True(dot < 0, $"triangle {t} winds the wrong way");
        }
    }

    static readonly uint Stone = Block.Solid(0x808080, Material.Stone);
    static readonly uint Glass = Block.Solid(0xC8E6F0, Material.Glass);

    [Fact]
    public void Opaque_face_next_to_glass_is_drawn()
    {
        var m = ChunkMesher.Build(World(blocks: new[] { (5, 5, 5, Stone), (6, 5, 5, Glass) }), 0, 0, 0).Opaque;
        bool found = false;
        for (int v = 0; v < m.VertexCount; v++)
            if (m.Normals[v * 3] > 0.5f && Math.Abs(m.Positions[v * 3] - 6 * 0.25f) < 1e-4) found = true;
        Assert.True(found);
    }

    [Fact]
    public void Faces_between_glass_blocks_are_culled() =>
        Assert.Equal(20, ChunkMesher.Build(World(blocks: new[] { (5, 5, 5, Glass), (6, 5, 5, Glass) }), 0, 0, 0).Glass.TriangleCount);

    [Fact]
    public void Glass_goes_to_its_own_mesh()
    {
        var m = ChunkMesher.Build(World(blocks: (5, 5, 5, Glass)), 0, 0, 0);
        Assert.Equal(0, m.Opaque.TriangleCount);
        Assert.Equal(12, m.Glass.TriangleCount);
    }

    [Fact]
    public void Vertices_carry_material_and_state()
    {
        var m = ChunkMesher.Build(World(blocks: (5, 5, 5, Block.WithState(Stone, BlockState.Cracked))), 0, 0, 0).Opaque;
        Assert.Equal(m.VertexCount * 2, m.Uv2.Count);
        for (int v = 0; v < m.VertexCount; v++)
        {
            Assert.Equal(6f, m.Uv2[v * 2]);
            Assert.Equal(1f, m.Uv2[v * 2 + 1]);
        }
    }

    [Fact]
    public void Glass_does_not_darken_neighbours()
    {
        var floor = new[] { (5, 4, 5, Stone), (6, 4, 5, Stone) };
        var without = ChunkMesher.Build(World(blocks: floor), 0, 0, 0).Opaque;
        var with = ChunkMesher.Build(World(blocks: floor.Append((6, 5, 5, Glass)).ToArray()), 0, 0, 0).Opaque;
        Assert.Equal(TopVertexReds(without, 1.5f, 1.25f, 1.25f), TopVertexReds(with, 1.5f, 1.25f, 1.25f));
    }
}
