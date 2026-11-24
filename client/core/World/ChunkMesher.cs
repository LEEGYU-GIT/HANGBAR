namespace Hangbar.Core.World;

/// <summary>
/// Engine-independent triangle mesh. Triangles wind clockwise seen from the front (Godot's convention).
/// Uv2 carries two floats per vertex: the block's material and state (the texture shader reads them).
/// </summary>
public sealed class MeshData
{
    public List<float> Positions { get; } = new();
    public List<float> Normals { get; } = new();
    public List<float> Colors { get; } = new();
    public List<float> Uv2 { get; } = new();
    public List<int> Indices { get; } = new();
    public int VertexCount => Positions.Count / 3;
    public int TriangleCount => Indices.Count / 3;
}

/// <summary>One chunk's meshes: opaque blocks, see-through glass, water surfaces.</summary>
public sealed record ChunkMeshes(MeshData Opaque, MeshData Glass, MeshData Water);

/// <summary>
/// Builds one chunk's meshes: visible faces only, per-vertex ambient occlusion from opaque neighbours.
/// Opaque faces show where the neighbour is not opaque (air, water, glass); glass faces where the neighbour is not solid.
/// </summary>
public static class ChunkMesher
{
    public const float BlockSize = 0.25f;
    static readonly float[] AoLight = { 1.0f, 0.8f, 0.65f, 0.5f };
    static readonly float[] WaterColor = { 0.25f, 0.55f, 0.8f, 0.7f };

    /// <summary>Face normal and its corners (unit-cube offsets), counter-clockwise seen from outside.</summary>
    static readonly (int[] N, int[][] C)[] Faces =
    {
        (new[] { 1, 0, 0 }, new[] { new[] { 1, 0, 0 }, new[] { 1, 1, 0 }, new[] { 1, 1, 1 }, new[] { 1, 0, 1 } }),
        (new[] { -1, 0, 0 }, new[] { new[] { 0, 0, 1 }, new[] { 0, 1, 1 }, new[] { 0, 1, 0 }, new[] { 0, 0, 0 } }),
        (new[] { 0, 1, 0 }, new[] { new[] { 0, 1, 0 }, new[] { 0, 1, 1 }, new[] { 1, 1, 1 }, new[] { 1, 1, 0 } }),
        (new[] { 0, -1, 0 }, new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { 1, 0, 1 }, new[] { 0, 0, 1 } }),
        (new[] { 0, 0, 1 }, new[] { new[] { 1, 0, 1 }, new[] { 1, 1, 1 }, new[] { 0, 1, 1 }, new[] { 0, 0, 1 } }),
        (new[] { 0, 0, -1 }, new[] { new[] { 0, 0, 0 }, new[] { 0, 1, 0 }, new[] { 1, 1, 0 }, new[] { 1, 0, 0 } }),
    };

    public static ChunkMeshes Build(VoxelWorld w, int cx, int cy, int cz)
    {
        var opaque = new MeshData();
        var glass = new MeshData();
        var water = new MeshData();
        int n = VoxelWorld.ChunkSize;
        for (int y = cy * n; y < Math.Min((cy + 1) * n, w.SizeY); y++)
            for (int z = cz * n; z < Math.Min((cz + 1) * n, w.SizeZ); z++)
                for (int x = cx * n; x < Math.Min((cx + 1) * n, w.SizeX); x++)
                {
                    uint b = w.Get(x, y, z);
                    if (Block.IsOpaque(b)) AddFaces(w, opaque, x, y, z, b, Block.IsOpaque);
                    else if (Block.IsSolid(b)) AddFaces(w, glass, x, y, z, b, Block.IsSolid);
                    else if (Block.IsWater(b) && w.Get(x, y + 1, z) == Block.Air) AddWaterTop(water, x, y, z);
                }
        return new ChunkMeshes(opaque, glass, water);
    }

    /// <summary>Faces of block <paramref name="block"/> whose neighbour does not satisfy <paramref name="hides"/>.</summary>
    static void AddFaces(VoxelWorld w, MeshData m, int x, int y, int z, uint block, Func<uint, bool> hides)
    {
        uint rgb = Block.ColorOf(block);
        float r = ((rgb >> 16) & 0xFF) / 255f, g = ((rgb >> 8) & 0xFF) / 255f, b = (rgb & 0xFF) / 255f;
        float mat = (float)Block.MaterialOf(block), state = (float)Block.StateOf(block);
        foreach (var (nrm, corners) in Faces)
        {
            int qx = x + nrm[0], qy = y + nrm[1], qz = z + nrm[2];
            if (hides(w.Get(qx, qy, qz))) continue;
            var ao = new int[4];
            for (int k = 0; k < 4; k++)
            {
                ao[k] = Occlusion(w, qx, qy, qz, nrm, corners[k]);
                var c = corners[k];
                float l = AoLight[ao[k]];
                m.Positions.AddRange(new[] { (x + c[0]) * BlockSize, (y + c[1]) * BlockSize, (z + c[2]) * BlockSize });
                m.Normals.AddRange(new float[] { nrm[0], nrm[1], nrm[2] });
                m.Colors.AddRange(new[] { r * l, g * l, b * l, 1f });
                m.Uv2.Add(mat);
                m.Uv2.Add(state);
            }
            AddQuad(m, ao[0] + ao[2] > ao[1] + ao[3]);
        }
    }

    /// <summary>Classic voxel AO from opaque cells: the two side cells and the corner cell next to this vertex, in front of the face.</summary>
    static int Occlusion(VoxelWorld w, int qx, int qy, int qz, int[] nrm, int[] corner)
    {
        int[] su = new int[3], sv = new int[3];
        bool first = true;
        for (int a = 0; a < 3; a++)
        {
            if (nrm[a] != 0) continue;
            var s = first ? su : sv;
            s[a] = corner[a] == 1 ? 1 : -1;
            first = false;
        }
        bool s1 = Block.IsOpaque(w.Get(qx + su[0], qy + su[1], qz + su[2]));
        bool s2 = Block.IsOpaque(w.Get(qx + sv[0], qy + sv[1], qz + sv[2]));
        bool c = Block.IsOpaque(w.Get(qx + su[0] + sv[0], qy + su[1] + sv[1], qz + su[2] + sv[2]));
        return s1 && s2 ? 3 : (s1 ? 1 : 0) + (s2 ? 1 : 0) + (c ? 1 : 0);
    }

    /// <summary>Two triangles over the last four vertices, clockwise; split along the other diagonal to avoid AO streaks.</summary>
    static void AddQuad(MeshData m, bool flip)
    {
        int v = m.VertexCount - 4;
        if (!flip) m.Indices.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
        else m.Indices.AddRange(new[] { v + 1, v + 3, v + 2, v + 1, v, v + 3 });
    }

    static void AddWaterTop(MeshData m, int x, int y, int z)
    {
        float top = (y + 1) * BlockSize - 0.1f * BlockSize;
        foreach (var c in Faces[2].C)
        {
            m.Positions.AddRange(new[] { (x + c[0]) * BlockSize, top, (z + c[2]) * BlockSize });
            m.Normals.AddRange(new[] { 0f, 1f, 0f });
            m.Colors.AddRange(WaterColor);
            m.Uv2.Add((float)Material.Water);
            m.Uv2.Add(0f);
        }
        AddQuad(m, false);
    }
}
