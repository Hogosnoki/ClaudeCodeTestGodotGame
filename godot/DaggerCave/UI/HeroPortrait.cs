using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// A hero on its title card: the 3D model idling under studio lights, rendered in a small
/// viewport with a world of its own (so the cave's lights and fog stay out of it) and drawn onto
/// the card like a picture.
/// </summary>
public partial class HeroPortrait : SubViewport
{
    /// <summary>The hero's design name ("swordsman" or "warden").</summary>
    public string Design = "swordsman";
    /// <summary>Only the chosen hero moves; the other holds still.</summary>
    public bool Playing;

    private CreatureModel _model;
    private float _time, _footY;
    private int _idleFrames = 12;

    public override void _Ready()
    {
        OwnWorld3D = true;
        TransparentBg = true;
        Msaa3D = Viewport.Msaa.Msaa4X;
        RenderTargetUpdateMode = UpdateMode.Always;

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.5f, 0.55f, 0.65f),
            AmbientLightEnergy = 0.4f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.1f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        // warm key from the front, a soft fill, cool rim from behind: the lighting the cave gives actors
        var key = new DirectionalLight3D { LightEnergy = 2.8f, LightColor = new Color(1f, 0.9f, 0.78f) };
        AddChild(key);
        key.LookAtFromPosition(new Vector3(2.5f, 3f, 4f), Vector3.Zero, Vector3.Up);
        var fill = new DirectionalLight3D { LightEnergy = 0.9f, LightColor = new Color(0.7f, 0.78f, 1f) };
        AddChild(fill);
        fill.LookAtFromPosition(new Vector3(-3f, 0.5f, 3f), Vector3.Zero, Vector3.Up);
        var rim = new DirectionalLight3D { LightEnergy = 2.4f, LightColor = new Color(0.55f, 0.72f, 1f) };
        AddChild(rim);
        rim.LookAtFromPosition(new Vector3(-3f, 2f, -3.5f), Vector3.Zero, Vector3.Up);

        _model = CreatureModel.Create(Design);
        if (_model == null) return;
        AddChild(_model);
        var set = SpriteSet.Get(Design);
        string idle = set.Names.Contains("idle_r") ? "idle_r" : "idle";
        _idleFrames = Math.Max(1, set.Frames.GetFrameCount(idle));

        // frame the whole figure, feet near the bottom edge
        var b = _model.Kit.Sculpt.Bounds;
        _footY = float.IsNaN(_model.Design.FloorY) ? b.Position.Y : _model.Design.FloorY;
        float h = b.End.Y - _footY;
        var cam = new Camera3D { Fov = 26f, Current = true };
        AddChild(cam);
        AddChild(new InkOutline(cam));
        float dist = h * 0.62f / MathF.Tan(Mathf.DegToRad(cam.Fov * 0.5f));
        var look = new Vector3(0, _footY + h * 0.5f, 0);
        cam.Position = look + new Vector3(0, h * 0.06f, dist);
        cam.LookAt(look, Vector3.Up);
        Pose(0f);
    }

    public override void _Process(double delta)
    {
        if (_model == null || RenderTargetUpdateMode == UpdateMode.Disabled) return;
        float dt = (float)delta;
        if (Playing) _time += dt;
        Pose(dt);
    }

    private void Pose(float dt)
    {
        float t = _time * 24f / _idleFrames % 1f;
        var a = new AnimInput
        {
            Clip = "idle", T = t, Frame = (int)(t * _idleFrames), Frames = _idleFrames, Loop = true,
            Time = _time, Dt = dt, Facing = 1, OnFloor = true, ClipTime = _time,
        };
        _model.Face(1, "idle", t, dt);
        _model.Animate(a);
        _model.UpdatePivot(Vector2.One, 0f, -_footY, false);
        // facing the card's text, turned much further toward us than in play
        var r = _model.Pivot.RotationDegrees;
        _model.Pivot.RotationDegrees = new Vector3(r.X, -58f, r.Z);
    }
}
