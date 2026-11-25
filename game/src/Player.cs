using Godot;

namespace Inpc.Game;

/// <summary>
/// Third-person walker (design §6-5). Origin at the feet. WASD move, Shift run, Space jump, mouse look
/// (click to capture, Esc to release). Steps up ledges of up to two blocks so 0.25 m terrain never snags.
/// </summary>
public partial class Player : CharacterBody3D
{
    const float Walk = 4.5f, Run = 7f, JumpSpeed = 5.5f, StepHeight = 0.55f;
    const float Radius = 0.3f, Tall = 1.75f;

    public bool InputEnabled { get; set; } = true;
    public Camera3D Camera { get; private set; } = null!;
    /// <summary>Set by tests/benches to walk without a keyboard: world-space direction on the ground plane.</summary>
    public Vector3? AutoWalk { get; set; }

    Node3D _yaw = null!;
    SpringArm3D _arm = null!;
    Node3D _figure = null!;
    float _gravity;

    public override void _Ready()
    {
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        // Flat-bottomed cylinder, not a capsule: a capsule's round bottom perches on 0.25 m ledge corners,
        // reads as "not on floor" and never triggers the step-up (found by --walktest on a staircase photo).
        AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = Radius, Height = Tall },
            Position = new Vector3(0, Tall / 2, 0),
        });
        _figure = Style.Soft ? new SoftFigure() : new Figure();
        _figure.Name = "Figure3D";
        AddChild(_figure);

        _yaw = new Node3D { Position = new Vector3(0, 1.6f, 0) };
        AddChild(_yaw);
        _arm = new SpringArm3D { SpringLength = 4.5f, Margin = 0.2f };
        _arm.AddExcludedObject(GetRid());
        _arm.RotationDegrees = new Vector3(-20, 0, 0);
        _yaw.AddChild(_arm);
        Camera = new Camera3D { Fov = 70, Far = 300 };
        _arm.AddChild(Camera);
        Camera.MakeCurrent();
        FloorSnapLength = 0.3f;
    }

    /// <summary>
    /// Turns the view: <paramref name="yawDegrees"/> uses Godot's convention (forward = (−sin, 0, −cos)), negative
    /// <paramref name="pitchDegrees"/> looks down. The camera stays behind the character on its spring arm.
    /// </summary>
    public void SetView(float yawDegrees, float pitchDegrees)
    {
        _yaw.RotationDegrees = new Vector3(0, yawDegrees, 0);
        _arm.RotationDegrees = new Vector3(Mathf.Clamp(pitchDegrees - 10, -70, 30), 0, 0);
        _figure.Rotation = new Vector3(0, Mathf.DegToRad(yawDegrees) - Rotation.Y, 0);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputEnabled) return;
        if (e is InputEventMouseButton { Pressed: true }) Input.MouseMode = Input.MouseModeEnum.Captured;
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) Input.MouseMode = Input.MouseModeEnum.Visible;
        else if (e is InputEventMouseMotion m && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw.RotateY(-m.Relative.X * 0.005f);
            var r = _arm.RotationDegrees;
            r.X = Mathf.Clamp(r.X - m.Relative.Y * 0.2f, -70, 30);
            _arm.RotationDegrees = r;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var v = Velocity;
        if (!IsOnFloor()) v.Y -= _gravity * dt;

        Vector3 wish = Vector3.Zero;
        bool run = false;
        if (AutoWalk is { } auto) wish = auto with { Y = 0 };
        else if (InputEnabled)
        {
            var input = new Vector2(
                (Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0),
                (Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0));
            var basis = _yaw.GlobalTransform.Basis;
            wish = basis.X * input.X + basis.Z * input.Y;
            wish.Y = 0;
            run = Input.IsKeyPressed(Key.Shift);
            if (Input.IsKeyPressed(Key.Space) && IsOnFloor()) v.Y = JumpSpeed;
        }
        if (wish.LengthSquared() > 1e-4f) wish = wish.Normalized() * (run ? Run : Walk);
        v.X = wish.X;
        v.Z = wish.Z;
        Velocity = v;

        if (IsOnFloor() && wish.LengthSquared() > 1e-4f) StepUp(wish * dt);
        MoveAndSlide();

        if (wish.LengthSquared() > 1e-4f)
            _figure.Rotation = new Vector3(0, Mathf.Atan2(-wish.X, -wish.Z) - Rotation.Y, 0);
    }

    /// <summary>
    /// Blocked by a ledge? Try: lift by StepHeight, move forward, drop back to the ground. If the ground found there
    /// is higher than now and no more than StepHeight up, stand on it before sliding.
    /// </summary>
    void StepUp(Vector3 move)
    {
        var t = GlobalTransform;
        if (!TestMove(t, move)) return;                       // nothing in the way
        var lift = new Vector3(0, StepHeight, 0);
        if (TestMove(t, lift)) return;                        // head room blocked
        var raised = t.Translated(lift);
        if (TestMove(raised, move)) return;                   // a wall, not a ledge
        var ahead = raised.Translated(move);
        var hit = new KinematicCollision3D();
        if (!TestMove(ahead, -lift, hit)) return;             // no ground within reach (a drop)
        var landed = ahead.Origin + hit.GetTravel();
        float rise = landed.Y - t.Origin.Y;
        if (rise > 0.01f && rise <= StepHeight) GlobalPosition = landed;
    }
}
