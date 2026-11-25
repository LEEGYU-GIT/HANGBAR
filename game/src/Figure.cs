using Godot;

namespace Inpc.Game;

/// <summary>The figure's faces.</summary>
public enum FigureFace { Normal, Dazed }

/// <summary>
/// The player character (soft style, user decision 2026-11-25): a soft figure that wobbles. Nothing is animated by hand-made clips: the body
/// leans on a spring toward where it is going and overshoots, the head and arms lag behind on their own springs,
/// legs swing with the distance walked, arms flail up in the air, the body squashes on landing. Faces −Z, about 1.6 m.
/// Look: one bean for head and body in the player's colour, simple eyes, a beanie, a backpack, noodle arms with mitts,
/// short legs with big boots (the common shape of co-op indie games, no particular game's character).
/// </summary>
public partial class Figure : Node3D
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
    Node3D _pupilL = null!, _pupilR = null!, _lidL = null!, _lidR = null!, _faceNormal = null!, _faceDazed = null!;

    /// <summary>Which face shows. Dazed is for later: hit hard, low health, a bad status (user idea 2026-11-25).</summary>
    public FigureFace Face { get; set; } = FigureFace.Normal;
    float _jig, _jigVel, _jigY, _jigYVel, _nextBlink = 2.2f;
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
        // the everyday face: dark oval eyes with a highlight, a small flat mouth
        _faceNormal = new Node3D();
        _head.AddChild(_faceNormal);
        foreach (float side in new[] { -1f, 1f })
        {
            _faceNormal.AddChild(Ball(0.075f, Ink, new Vector3(side * 0.13f, 0.02f, -0.33f), new Vector3(0.85f, 1.35f, 0.5f)));
            _faceNormal.AddChild(Ball(0.026f, Colors.White, new Vector3(side * 0.13f + 0.02f, 0.06f, -0.37f)));
        }
        _faceNormal.AddChild(Capsule(0.018f, 0.12f, Ink, new Vector3(0, -0.12f, -0.355f), new Vector3(0, 0, Mathf.Pi / 2)));

        // the dazed face (hit hard, low health, a bad status): googly eyes of two sizes looking different ways,
        // sleepy lids, a lopsided open mouth, one buck tooth
        _faceDazed = new Node3D();
        _head.AddChild(_faceDazed);
        (_pupilL, _lidL) = Eye(new Vector3(-0.125f, 0.035f, -0.3f), 0.1f);
        (_pupilR, _lidR) = Eye(new Vector3(0.13f, 0.0f, -0.315f), 0.078f);
        _faceDazed.AddChild(Ball(0.05f, Ink, new Vector3(0.05f, -0.135f, -0.345f), new Vector3(1.25f, 0.75f, 0.35f)));
        _faceDazed.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.034f, 0.036f, 0.012f) }, MaterialOverride = Mat(Colors.White),
            Position = new Vector3(0.04f, -0.112f, -0.362f),
        });
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

    /// <summary>A white eyeball with a small pupil on a pivot (to roll it) and a lid on a pivot (to blink).</summary>
    (Node3D Pupil, Node3D Lid) Eye(Vector3 at, float r)
    {
        _faceDazed.AddChild(Ball(r, Colors.White, at));
        var pupil = new Node3D { Position = at };
        _faceDazed.AddChild(pupil);
        pupil.AddChild(Ball(r * 0.36f, Ink, new Vector3(0, 0, -r * 0.88f), new Vector3(1, 1, 0.4f)));
        var lid = new Node3D { Position = at };
        _faceDazed.AddChild(lid);
        var lidMesh = Ball(r * 1.1f, Tint.Darkened(0.08f), Vector3.Zero);
        lidMesh.Mesh = new SphereMesh { Radius = r * 1.1f, Height = r * 1.1f, IsHemisphere = true, RadialSegments = 20, Rings = 6 };
        lid.AddChild(lidMesh);
        return (pupil, lid);
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
        float speed = flat.Length(), walk = Mathf.Clamp(speed / 3f, 0, 1.6f);
        var accel = (flat - new Vector3(_lastVel.X, 0, _lastVel.Z)) / Mathf.Max(dt, 1e-3f);
        var forward = -GlobalTransform.Basis.Z;
        float push = accel.Dot(forward) * 0.012f;
        float yaw = GlobalRotation.Y, turn = Mathf.Wrap(yaw - _lastYaw, -Mathf.Pi, Mathf.Pi) / Mathf.Max(dt, 1e-3f);

        // body: leans into the walk and the push, rolls into turns; soft springs so it wobbles
        Spring(ref _lean, ref _leanVel, -(0.18f * walk + Mathf.Clamp(push, -0.3f, 0.3f)), 70f, 7f, dt);
        Spring(ref _roll, ref _rollVel, Mathf.Clamp(-turn * 0.04f * walk, -0.35f, 0.35f), 60f, 6f, dt);
        _phase += speed * dt * Mathf.Pi / 0.5f; // one step per 0.5 m: short legs take quick little steps
        float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * Mathf.Min(walk, 1f);
        _hips.Position = new Vector3(0, HipY + bob, 0);
        _body.Rotation = new Vector3(_lean, 0, _roll + Mathf.Sin(_phase) * 0.06f * Mathf.Min(walk, 1f));

        // head lags behind the body
        Spring(ref _headLag, ref _headLagVel, -_leanVel * 0.06f, 90f, 6f, dt);
        _head.Rotation = new Vector3(_headLag + Mathf.Sin(_time * 1.7f) * 0.03f, 0, -_rollVel * 0.02f);

        _faceNormal.Visible = Face == FigureFace.Normal;
        _faceDazed.Visible = Face == FigureFace.Dazed;
        // googly pupils slosh behind every move; the left looks down and out, the right drifts in and down
        Spring(ref _jig, ref _jigVel, Mathf.Clamp(-_rollVel * 0.3f + Mathf.Sin(_time * 0.6f) * 0.12f, -0.6f, 0.6f), 22f, 2.2f, dt);
        Spring(ref _jigY, ref _jigYVel, Mathf.Clamp(_leanVel * 0.35f + (onFloor ? 0 : 0.45f), -0.6f, 0.6f), 22f, 2.2f, dt);
        _pupilL.Rotation = new Vector3(-0.38f + _jigY * 0.5f, 0.45f + _jig, 0);
        _pupilR.Rotation = new Vector3(-0.28f + _jigY * 0.7f, 0.22f + _jig * 1.3f, 0);
        // sleepy lids, the left lower than the right; a slow blink now and then
        float blink = 0;
        if (_time > _nextBlink)
        {
            float t = (_time - _nextBlink) / 0.22f;
            blink = t < 1 ? Mathf.Sin(t * Mathf.Pi) : 0;
            if (t >= 1) _nextBlink = _time + 2.5f + 2.5f * (Mathf.Sin(_time * 12.9898f) * 0.5f + 0.5f);
        }
        _lidL.Rotation = new Vector3(-0.42f - blink * 1.2f, 0, 0.12f);
        _lidR.Rotation = new Vector3(-0.3f - blink * 1.3f, 0, -0.1f);

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
