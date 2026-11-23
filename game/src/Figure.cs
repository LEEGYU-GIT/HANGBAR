using Godot;

namespace Inpc.Game;

/// <summary>
/// The goofy round biped, 1.75 m tall, built from primitive shapes (design §6-5). Faces -Z.
/// Proportions follow the scenic test scene's character.
/// </summary>
public partial class Figure : Node3D
{
    public override void _Ready()
    {
        var skin = new StandardMaterial3D { AlbedoColor = new Color(1.0f, 0.86f, 0.74f), Roughness = 0.8f };
        var shirt = new StandardMaterial3D { AlbedoColor = new Color(0.91f, 0.38f, 0.17f), Roughness = 0.7f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.12f), Roughness = 0.6f };
        var leg = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 0.55f };
        var arm = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.55f };
        var ball = new SphereMesh();

        Part(leg, dark, new Vector3(-0.12f, 0.28f, 0));
        Part(leg, dark, new Vector3(0.12f, 0.28f, 0), rot: new Vector3(0, 0, 0.2f));
        Part(ball, shirt, new Vector3(0, 0.88f, 0), new Vector3(0.6f, 0.8f, 0.55f));
        Part(ball, skin, new Vector3(0, 1.42f, 0.02f), new Vector3(0.66f, 0.66f, 0.66f));
        Part(ball, dark, new Vector3(-0.11f, 1.48f, -0.3f), new Vector3(0.09f, 0.09f, 0.09f));
        Part(ball, dark, new Vector3(0.13f, 1.45f, -0.3f), new Vector3(0.09f, 0.09f, 0.09f));
        Part(arm, skin, new Vector3(-0.32f, 1.05f, 0), rot: new Vector3(0, 0, -0.5f));
        Part(arm, skin, new Vector3(0.32f, 1.05f, 0), rot: new Vector3(0, 0, 0.5f));
    }

    void Part(Mesh mesh, Material mat, Vector3 pos, Vector3? scale = null, Vector3? rot = null) =>
        AddChild(new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = mat, Position = pos,
            Scale = scale ?? Vector3.One, Rotation = rot ?? Vector3.Zero,
        });
}
