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
/// legs swing with the distance walked, arms flail up in the air, the body squashes on landing. Faces −Z, 1.75 m.
/// </summary>
public partial class SoftFigure : Node3D
{
    static readonly Color Body = new(0.32f, 0.6f, 1f), Skin = new(1f, 0.8f, 0.66f), Ink = new(0.12f, 0.1f, 0.12f),
        Cheek = new(1f, 0.5f, 0.55f), Shoe = new(0.98f, 0.8f, 0.3f);

    Node3D _hips = null!, _body = null!, _head = null!, _armL = null!, _armR = null!, _legL = null!, _legR = null!;
    CharacterBody3D? _owner;
    float _lean, _leanVel, _roll, _rollVel, _headLag, _headLagVel, _armSwing, _armSwingVel, _flail, _flailVel;
    float _squash, _squashVel, _phase, _time, _lastYaw;
    Vector3 _lastVel;
    bool _wasOnFloor = true;

    static StandardMaterial3D Mat(Color c, float rough = 0.85f) => new() { AlbedoColor = c, Roughness = rough };

    static MeshInstance3D Ball(float r, Color c, Vector3 pos, Vector3? scale = null) => new()
    {
        Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 24, Rings = 12 }, MaterialOverride = Mat(c),
        Position = pos, Scale = scale ?? Vector3.One,
    };

    static MeshInstance3D Capsule(float r, float h, Color c, Vector3 pos) => new()
    {
        Mesh = new CapsuleMesh { Radius = r, Height = h, RadialSegments = 20, Rings = 6 }, MaterialOverride = Mat(c), Position = pos,
    };

    public override void _Ready()
    {
        _owner = GetParent() as CharacterBody3D;
        _hips = new Node3D { Position = new Vector3(0, 0.48f, 0) };
        AddChild(_hips);

        // legs hang from the hips and swing
        _legL = Limb(_hips, new Vector3(-0.14f, 0, 0), 0.11f, 0.5f, Shoe);
        _legR = Limb(_hips, new Vector3(0.14f, 0, 0), 0.11f, 0.5f, Shoe);

        // the body leans from the hips; the head sits on it with its own lag
        _body = new Node3D();
        _hips.AddChild(_body);
        _body.AddChild(Ball(0.34f, Body, new Vector3(0, 0.3f, 0), new Vector3(1f, 1.15f, 0.9f)));
        _head = new Node3D { Position = new Vector3(0, 0.62f, 0) };
        _body.AddChild(_head);
        _head.AddChild(Ball(0.4f, Skin, new Vector3(0, 0.32f, 0)));
        foreach (float side in new[] { -1f, 1f })
        {
            _head.AddChild(Ball(0.085f, Colors.White, new Vector3(side * 0.14f, 0.38f, -0.34f), new Vector3(1, 1.2f, 0.6f)));
            _head.AddChild(Ball(0.05f, Ink, new Vector3(side * 0.14f, 0.37f, -0.39f), new Vector3(1, 1.2f, 0.6f)));
            _head.AddChild(Ball(0.07f, Cheek, new Vector3(side * 0.24f, 0.24f, -0.3f), new Vector3(1, 0.6f, 0.4f)));
        }
        _armL = Limb(_body, new Vector3(-0.36f, 0.42f, 0), 0.09f, 0.48f, Body);
        _armR = Limb(_body, new Vector3(0.36f, 0.42f, 0), 0.09f, 0.48f, Body);
    }

    /// <summary>A pivot with a capsule hanging down from it.</summary>
    static Node3D Limb(Node3D parent, Vector3 at, float r, float length, Color c)
    {
        var pivot = new Node3D { Position = at };
        parent.AddChild(pivot);
        pivot.AddChild(Capsule(r, length, c, new Vector3(0, -length / 2 + r * 0.5f, 0)));
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
        _hips.Position = new Vector3(0, 0.48f + bob, 0);
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
