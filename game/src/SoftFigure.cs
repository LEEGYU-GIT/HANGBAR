using Godot;

namespace Inpc.Game;

/// <summary>Which look the game uses. SPIKE: --style=soft switches to the soft test look.</summary>
public static class Style
{
    public static bool Soft { get; set; }
}

/// <summary>
/// SPIKE (style test): a soft, big-headed figure that wobbles. Nothing is animated by hand-made clips: the body
/// leans on a spring toward where it is going and overshoots, the head and arms lag behind on their own springs,
/// legs swing with the distance walked, arms flail up in the air, the body squashes on landing. Faces −Z, about 1.6 m.
/// Look: one bean for head and body in the player's colour, simple eyes, a beanie, a backpack, noodle arms with mitts,
/// short legs with big boots (the common shape of co-op indie games, no particular game's character).
/// </summary>
public partial class SoftFigure : Node3D
{
    const float HipY = 0.34f;
    static readonly Color Ink = new(0.1f, 0.09f, 0.12f), Mitt = new(0.97f, 0.95f, 0.9f), Boot = new(0.27f, 0.22f, 0.24f),
        Pack = new(0.55f, 0.4f, 0.28f);

    /// <summary>The player's colour (body and arms). Co-op players differ by colour and hat.</summary>
    public Color Tint { get; set; } = new(0.98f, 0.5f, 0.28f);
    public Color HatColor { get; set; } = new(0.2f, 0.45f, 0.85f);

    Node3D _hips = null!, _body = null!, _head = null!, _armL = null!, _armR = null!, _legL = null!, _legR = null!;
    CharacterBody3D? _owner;
    float _lean, _leanVel, _roll, _rollVel, _headLag, _headLagVel, _armSwing, _armSwingVel, _flail, _flailVel;
    float _squash, _squashVel, _phase, _time, _lastYaw;
    Vector3 _lastVel;
    bool _wasOnFloor = true;

    static StandardMaterial3D Mat(Color c, float rough = 0.8f) => new() { AlbedoColor = c, Roughness = rough };

    static MeshInstance3D Ball(float r, Color c, Vector3 pos, Vector3? scale = null) => new()
    {
        Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 24, Rings = 12 }, MaterialOverride = Mat(c),
        Position = pos, Scale = scale ?? Vector3.One,
    };

    static MeshInstance3D Capsule(float r, float h, Color c, Vector3 pos, Vector3? rot = null) => new()
    {
        Mesh = new CapsuleMesh { Radius = r, Height = h, RadialSegments = 20, Rings = 6 }, MaterialOverride = Mat(c), Position = pos,
        Rotation = rot ?? Vector3.Zero,
    };

    public override void _Ready()
    {
        _owner = GetParent() as CharacterBody3D;
        _hips = new Node3D { Position = new Vector3(0, HipY, 0) };
        AddChild(_hips);

        // short legs with big boots
        _legL = Leg(_hips, -0.15f);
        _legR = Leg(_hips, 0.15f);

        // one bean for head and body, leaning from the hips
        _body = new Node3D();
        _hips.AddChild(_body);
        _body.AddChild(Capsule(0.36f, 1.12f, Tint, new Vector3(0, 0.5f, 0)));
        // a backpack that bounces with the body
        var pack = Capsule(0.2f, 0.5f, Pack, new Vector3(0, 0.45f, 0.36f), new Vector3(0, 0, Mathf.Pi / 2));
        pack.Scale = new Vector3(1, 1.25f, 0.8f);
        _body.AddChild(pack);

        // face and hat ride on their own lagging pivot near the top of the bean
        _head = new Node3D { Position = new Vector3(0, 0.78f, 0) };
        _body.AddChild(_head);
        foreach (float side in new[] { -1f, 1f })
        {
            _head.AddChild(Ball(0.075f, Ink, new Vector3(side * 0.13f, 0.02f, -0.33f), new Vector3(0.85f, 1.35f, 0.5f)));
            _head.AddChild(Ball(0.026f, Colors.White, new Vector3(side * 0.13f + 0.02f, 0.06f, -0.37f)));
        }
        _head.AddChild(Capsule(0.018f, 0.12f, Ink, new Vector3(0, -0.12f, -0.355f), new Vector3(0, 0, Mathf.Pi / 2)));
        // beanie: a cap, a folded rim and a pompom
        var cap = Ball(0.375f, HatColor, new Vector3(0, 0.1f, 0), new Vector3(1, 0.75f, 1));
        cap.Mesh = new SphereMesh { Radius = 0.375f, Height = 0.375f, IsHemisphere = true, RadialSegments = 24, Rings = 8 };
        _head.AddChild(cap);
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.33f, OuterRadius = 0.41f, Rings = 24, RingSegments = 10 },
            MaterialOverride = Mat(HatColor.Darkened(0.15f)), Position = new Vector3(0, 0.12f, 0),
        });
        _head.AddChild(Ball(0.09f, Mitt, new Vector3(0, 0.42f, 0)));

        // noodle arms with mitts
        _armL = Arm(_body, -1);
        _armR = Arm(_body, 1);
    }

    Node3D Leg(Node3D parent, float x)
    {
        var pivot = new Node3D { Position = new Vector3(x, 0, 0) };
        parent.AddChild(pivot);
        pivot.AddChild(Capsule(0.09f, 0.3f, Tint.Darkened(0.35f), new Vector3(0, -0.12f, 0)));
        pivot.AddChild(Ball(0.13f, Boot, new Vector3(0, -0.27f, -0.05f), new Vector3(1, 0.65f, 1.35f)));
        return pivot;
    }

    Node3D Arm(Node3D parent, float side)
    {
        var pivot = new Node3D { Position = new Vector3(side * 0.36f, 0.42f, 0) };
        parent.AddChild(pivot);
        pivot.AddChild(Capsule(0.055f, 0.48f, Tint, new Vector3(0, -0.22f, 0)));
        pivot.AddChild(Ball(0.1f, Mitt, new Vector3(0, -0.47f, 0)));
        return pivot;
    }

    /// <summary>One damped spring step: value chases target, overshooting a little.</summary>
    static void Spring(ref float value, ref float vel, float target, float stiffness, float damping, float dt)
    {
        vel += ((target - value) * stiffness - vel * damping) * dt;
        value += vel * dt;
    }

    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 1f / 30f);
        _time += dt;
        var v = _owner?.Velocity ?? Vector3.Zero;
        bool onFloor = _owner?.IsOnFloor() ?? true;
        var flat = new Vector3(v.X, 0, v.Z);
        float speed = flat.Length(), walk = Mathf.Clamp(speed / 4.5f, 0, 1.6f);
        var accel = (flat - new Vector3(_lastVel.X, 0, _lastVel.Z)) / Mathf.Max(dt, 1e-3f);
        var forward = -GlobalTransform.Basis.Z;
        float push = accel.Dot(forward) * 0.012f;
        float yaw = GlobalRotation.Y, turn = Mathf.Wrap(yaw - _lastYaw, -Mathf.Pi, Mathf.Pi) / Mathf.Max(dt, 1e-3f);

        // body: leans into the walk and the push, rolls into turns; soft springs so it wobbles
        Spring(ref _lean, ref _leanVel, -(0.18f * walk + Mathf.Clamp(push, -0.3f, 0.3f)), 70f, 7f, dt);
        Spring(ref _roll, ref _rollVel, Mathf.Clamp(-turn * 0.04f * walk, -0.35f, 0.35f), 60f, 6f, dt);
        _phase += speed * dt * 2.4f;
        float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * Mathf.Min(walk, 1f);
        _hips.Position = new Vector3(0, HipY + bob, 0);
        _body.Rotation = new Vector3(_lean, 0, _roll + Mathf.Sin(_phase) * 0.06f * Mathf.Min(walk, 1f));

        // head lags behind the body
        Spring(ref _headLag, ref _headLagVel, -_leanVel * 0.06f, 90f, 6f, dt);
        _head.Rotation = new Vector3(_headLag + Mathf.Sin(_time * 1.7f) * 0.03f, 0, -_rollVel * 0.02f);

        // legs swing with distance; tucked in the air
        float stride = Mathf.Sin(_phase) * 0.7f * Mathf.Min(walk, 1.2f);
        float tuck = onFloor ? 0 : 0.5f;
        _legL.Rotation = new Vector3(stride - tuck, 0, 0);
        _legR.Rotation = new Vector3(-stride - tuck, 0, 0);

        // arms: swing against the legs with lag, flail up and out in the air or when running
        Spring(ref _armSwing, ref _armSwingVel, -stride * 0.9f, 50f, 5f, dt);
        float flailTarget = onFloor ? 0.25f + 0.35f * Mathf.Max(0, walk - 1f) : 1.9f;
        Spring(ref _flail, ref _flailVel, flailTarget, 40f, 4f, dt);
        float wiggle = onFloor ? 0 : Mathf.Sin(_time * 14f) * 0.25f;
        _armL.Rotation = new Vector3(_armSwing, 0, -_flail - wiggle);
        _armR.Rotation = new Vector3(-_armSwing, 0, _flail + wiggle);

        // squash on landing, stretch while rising, a slow breath standing still
        if (onFloor && !_wasOnFloor) _squashVel -= Mathf.Clamp(-_lastVel.Y, 0, 12f) * 0.9f;
        float stretch = onFloor ? 0 : Mathf.Clamp(v.Y * 0.02f, -0.08f, 0.1f);
        Spring(ref _squash, ref _squashVel, stretch, 120f, 9f, dt);
        float breath = Mathf.Sin(_time * 2.2f) * 0.015f * (1 - Mathf.Min(walk, 1f));
        float sy = 1 + _squash + breath, sxz = 1 - _squash * 0.6f;
        _hips.Scale = new Vector3(sxz, sy, sxz);

        _lastVel = v;
        _lastYaw = yaw;
        _wasOnFloor = onFloor;
    }
}
