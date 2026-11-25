using Godot;
using Hangbar.Core.World;

namespace Inpc.Game;

/// <summary>
/// A voxel world as chunk nodes: opaque blocks with the block texture shader and a collider, see-through glass with a
/// collider, water surfaces without one. Used by both the G0 path and the v2 path.
/// </summary>
public static class WorldView
{
    public static (int Triangles, int Chunks) Build(Node3D parent, VoxelWorld world)
    {
        var solidMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/block.gdshader") };
        // Block colours come straight from the photo, so they are sRGB, not linear.
        var waterMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.1f,
        };
        var glassMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.35f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.05f,
        };
        int tris = 0, chunks = 0;
        for (int cy = 0; cy < world.ChunksY; cy++)
            for (int cz = 0; cz < world.ChunksZ; cz++)
                for (int cx = 0; cx < world.ChunksX; cx++)
                {
                    var built = ChunkMesher.Build(world, cx, cy, cz);
                    if (built.Opaque.TriangleCount > 0)
                    {
                        var mi = new MeshInstance3D { Mesh = ToMesh(built.Opaque), MaterialOverride = solidMat, Name = $"C{cx}_{cy}_{cz}" };
                        mi.AddChild(Collider(built.Opaque));
                        parent.AddChild(mi);
                        chunks++;
                        tris += built.Opaque.TriangleCount;
                    }
                    if (built.Glass.TriangleCount > 0)
                    {
                        var gi = new MeshInstance3D
                        {
                            Mesh = ToMesh(built.Glass), MaterialOverride = glassMat, Name = $"G{cx}_{cy}_{cz}",
                            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                        };
                        gi.AddChild(Collider(built.Glass));
                        parent.AddChild(gi);
                        tris += built.Glass.TriangleCount;
                    }
                    if (built.Water.TriangleCount > 0)
                    {
                        parent.AddChild(new MeshInstance3D
                        {
                            Mesh = ToMesh(built.Water), MaterialOverride = waterMat, Name = $"W{cx}_{cy}_{cz}",
                            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                        });
                        tris += built.Water.TriangleCount;
                    }
                }
        return (tris, chunks);
    }

    static StaticBody3D Collider(MeshData m)
    {
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = Faces(m), BackfaceCollision = true } });
        return body;
    }

    static ArrayMesh ToMesh(MeshData m)
    {
        int n = m.VertexCount;
        var pos = new Vector3[n];
        var nrm = new Vector3[n];
        var col = new Color[n];
        var uv2 = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
            nrm[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
            col[i] = new Color(m.Colors[i * 4], m.Colors[i * 4 + 1], m.Colors[i * 4 + 2], m.Colors[i * 4 + 3]);
            if (m.Uv2.Count == n * 2) uv2[i] = new Vector2(m.Uv2[i * 2], m.Uv2[i * 2 + 1]); // material, state
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = pos;
        arrays[(int)Mesh.ArrayType.Normal] = nrm;
        arrays[(int)Mesh.ArrayType.Color] = col;
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2;
        arrays[(int)Mesh.ArrayType.Index] = m.Indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    static Vector3[] Faces(MeshData m)
    {
        var f = new Vector3[m.Indices.Count];
        for (int i = 0; i < f.Length; i++)
        {
            int v = m.Indices[i];
            f[i] = new Vector3(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2]);
        }
        return f;
    }
}
