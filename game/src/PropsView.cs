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
    sealed record Piece(Mesh Mesh, Vector3 Offset, Vector3 Scale, Color? Tint = null, Vector3 Turn = default);

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
                    mm.SetInstanceTransform(i, new Transform3D(basis, feet + piece.Offset * p.Scale));
                    mm.SetInstanceColor(i, piece.Tint ?? Color.Color8((byte)(p.Color >> 16), (byte)(p.Color >> 8), (byte)p.Color));
                }
                parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.On, Name = $"Props{group.Key}" });
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
