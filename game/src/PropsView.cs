using Godot;
using Hangbar.Core.World;

namespace Inpc.Game;

/// <summary>
/// Props as round shapes (design §4-2: everything that is not terrain is round): one MultiMeshInstance3D per shape
/// piece, instance colours from the prop. No colliders yet (G0.5 Task 6 decides). Tree sizes must match
/// Hangbar.Core.World.Gen.PropRules (crown centre 2 m × scale up, radius 0.9 m × scale).
/// </summary>
public static class PropsView
{
    /// <summary>One piece of a prop's look: a mesh, its offset above the prop's feet and its scale, in metres for scale 1.</summary>
    sealed record Piece(Mesh Mesh, Vector3 Offset, Vector3 Scale, Color? Tint = null, Vector3 Turn = default, float Shade = 1f, float Sway = 0f);

    /// <summary>SPIKE (style test): chunkier, faceted round shapes; trees are clusters of blobs that sway.</summary>
    static void SoftLooks(Dictionary<PropKind, Piece[]> looks)
    {
        static SphereMesh Blob(float r) => new() { Radius = r, Height = r * 2, RadialSegments = 9, Rings = 5 };
        var bark = new Color(0.5f, 0.34f, 0.24f);
        looks[PropKind.Tree] = new[]
        {
            new Piece(new CylinderMesh { RadialSegments = 7, Rings = 1, TopRadius = 0.09f, BottomRadius = 0.17f, Height = 1.6f }, new Vector3(0, 0.8f, 0), Vector3.One, bark),
            new Piece(Blob(0.85f), new Vector3(0, 2.15f, 0), new Vector3(1, 0.9f, 1), Sway: 0.07f),
            new Piece(Blob(0.6f), new Vector3(0.55f, 1.85f, 0.15f), Vector3.One, Shade: 0.88f, Sway: 0.07f),
            new Piece(Blob(0.62f), new Vector3(-0.5f, 1.9f, -0.25f), Vector3.One, Shade: 1.1f, Sway: 0.07f),
            new Piece(Blob(0.5f), new Vector3(0.1f, 2.75f, -0.1f), Vector3.One, Shade: 1.15f, Sway: 0.07f),
        };
        looks[PropKind.Bush] = new[]
        {
            new Piece(Blob(0.42f), new Vector3(0, 0.3f, 0), Vector3.One, Sway: 0.03f),
            new Piece(Blob(0.3f), new Vector3(0.35f, 0.22f, 0.1f), Vector3.One, Shade: 1.12f, Sway: 0.03f),
        };
        looks[PropKind.Rock] = new[] { new Piece(new SphereMesh { Radius = 0.4f, Height = 0.8f, RadialSegments = 6, Rings = 3 }, new Vector3(0, 0.12f, 0), new Vector3(1.1f, 0.65f, 0.9f)) };
        looks[PropKind.Parasol] = new[]
        {
            new Piece(new CylinderMesh { RadialSegments = 7, Rings = 1, TopRadius = 0.03f, BottomRadius = 0.035f, Height = 2.1f }, new Vector3(0, 1.05f, 0), Vector3.One, new Color(0.97f, 0.95f, 0.9f)),
            new Piece(new SphereMesh { RadialSegments = 10, Rings = 3, Radius = 1.0f, Height = 1.0f, IsHemisphere = true }, new Vector3(0, 1.95f, 0), new Vector3(1, 0.4f, 1), Sway: 0.015f),
        };
    }

    public static int Build(Node3D parent, VoxelWorld world, IReadOnlyList<Prop> props)
    {
        var looks = new Dictionary<PropKind, Piece[]>
        {
            [PropKind.Tree] = new[]
            {
                new Piece(new CylinderMesh { RadialSegments = 8, Rings = 1, TopRadius = 0.1f, BottomRadius = 0.14f, Height = 1.5f }, new Vector3(0, 0.75f, 0), Vector3.One, new Color(0.36f, 0.25f, 0.16f)),
                new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.9f, Height = 1.6f }, new Vector3(0, 2.0f, 0), Vector3.One),
            },
            [PropKind.Bush] = new[] { new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.45f, Height = 0.7f }, new Vector3(0, 0.3f, 0), Vector3.One) },
            [PropKind.Rock] = new[] { new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.4f, Height = 0.8f }, new Vector3(0, 0.15f, 0), new Vector3(1, 0.6f, 0.85f)) },
            [PropKind.Cup] = new[] { new Piece(new CylinderMesh { RadialSegments = 8, Rings = 1, TopRadius = 0.045f, BottomRadius = 0.035f, Height = 0.1f }, new Vector3(0, 0.05f, 0), Vector3.One) },
            [PropKind.Plant] = new[]
            {
                new Piece(new CylinderMesh { RadialSegments = 8, Rings = 1, TopRadius = 0.1f, BottomRadius = 0.08f, Height = 0.15f }, new Vector3(0, 0.075f, 0), Vector3.One, new Color(0.55f, 0.32f, 0.22f)),
                new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.16f, Height = 0.3f }, new Vector3(0, 0.27f, 0), Vector3.One, new Color(0.3f, 0.55f, 0.25f)),
            },
            [PropKind.Lamp] = new[]
            {
                new Piece(new CylinderMesh { RadialSegments = 8, Rings = 1, TopRadius = 0.015f, BottomRadius = 0.05f, Height = 0.3f }, new Vector3(0, 0.15f, 0), Vector3.One, new Color(0.25f, 0.25f, 0.27f)),
                new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.1f, Height = 0.2f }, new Vector3(0, 0.35f, 0), Vector3.One),
            },
            [PropKind.Bench] = new[] { new Piece(new CapsuleMesh { RadialSegments = 10, Rings = 3, Radius = 0.2f, Height = 1.4f }, new Vector3(0, 0.4f, 0), Vector3.One, null, new Vector3(0, 0, Mathf.Pi / 2)) },
            [PropKind.Parasol] = new[]
            {
                new Piece(new CylinderMesh { RadialSegments = 8, Rings = 1, TopRadius = 0.025f, BottomRadius = 0.03f, Height = 2.1f }, new Vector3(0, 1.05f, 0), Vector3.One, new Color(0.9f, 0.9f, 0.88f)),
                new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 1.0f, Height = 1.0f, IsHemisphere = true }, new Vector3(0, 1.95f, 0), new Vector3(1, 0.35f, 1)),
            },
            [PropKind.Float] = new[] { new Piece(new SphereMesh { RadialSegments = 12, Rings = 6, Radius = 0.12f, Height = 0.24f }, new Vector3(0, 0.02f, 0), Vector3.One) },
        };
        var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f };
        var softShader = Style.Soft ? GD.Load<Shader>("res://shaders/soft_prop.gdshader") : null;
        if (Style.Soft) SoftLooks(looks);
        Godot.Material MaterialFor(Piece piece)
        {
            if (softShader is null) return mat;
            var m = new ShaderMaterial { Shader = softShader };
            m.SetShaderParameter("sway", piece.Sway);
            return m;
        }
        float bs = ChunkMesher.BlockSize;
        int count = 0;
        foreach (var group in props.GroupBy(p => p.Kind))
        {
            if (!looks.TryGetValue(group.Key, out var pieces)) continue;
            var list = group.ToList();
            count += list.Count;
            foreach (var piece in pieces)
            {
                var mm = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = piece.Mesh,
                    InstanceCount = list.Count,
                };
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    int y = p.Y >= 0 ? p.Y : TopFree(world, p.X, p.Z);
                    var feet = new Vector3((p.X + 0.5f) * bs, y * bs, (p.Z + 0.5f) * bs);
                    float yaw = (p.X * 7 + p.Z * 13) % 360 * Mathf.Pi / 180;
                    var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromScale(piece.Scale * p.Scale) * Basis.FromEuler(piece.Turn);
                    var offset = Basis.FromEuler(new Vector3(0, yaw, 0)) * (piece.Offset * p.Scale);
                    mm.SetInstanceTransform(i, new Transform3D(basis, feet + offset));
                    var c = piece.Tint ?? Color.Color8((byte)(p.Color >> 16), (byte)(p.Color >> 8), (byte)p.Color);
                    mm.SetInstanceColor(i, new Color(Mathf.Min(1, c.R * piece.Shade), Mathf.Min(1, c.G * piece.Shade), Mathf.Min(1, c.B * piece.Shade)));
                }
                parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = MaterialFor(piece), CastShadow = GeometryInstance3D.ShadowCastingSetting.On, Name = $"Props{group.Key}" });
            }
        }
        return count;
    }

    static int TopFree(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--)
            if (Block.IsSolid(w.Get(x, y, z))) return y + 1;
        return 0;
    }
}
