using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Automaton, disabled: his own model collapsed in a heap on the floor (the last frame of a fall), a faint blue spark
/// jumping at his joints, his eyes dark. Revived, he gets up out of the heap and stands.
/// </summary>
public partial class AutomatonHeapView : PropView
{
    private CreatureModel _model;
    private OmniLight3D _light;
    private float _footY;

    protected override void Build()
    {
        _model = CreatureModel.Create(Player.SheetName(HeroKind.Automaton));
        if (_model == null) return;
        AddChild(_model);
        _footY = float.IsNaN(_model.Design.FloorY) ? _model.Kit.Sculpt.Bounds.Position.Y : _model.Design.FloorY;
        // (no lantern lit in a dead machine)
        foreach (var l in _model.FindChildren("*", nameof(OmniLight3D), true, false)) ((OmniLight3D)l).Visible = false;
        _light = PropViews.Light(new Color(0.6f, 0.8f, 1f), 0f, 2.6f);
        _light.Position = new Vector3(0, 0.3f, 0.4f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        if (_model == null) return;
        var c = (HeroCage)Owner2D;
        Follow(default, 0f);
        // collapsed (the end of the fall), and once revived, rising out of it over a second and a half
        float rise = c.Freed ? Math.Clamp(c.FreedT / 1.5f, 0f, 1f) : 0f;
        string clip = rise < 1f ? "death" : "idle";
        var a = new AnimInput { Clip = clip, T = 1f - W3.Smooth01(rise), Frame = 0, Frames = 1, Loop = clip == "idle", Time = Time, Dt = dt, Facing = 1, OnFloor = true, ClipTime = Time };
        _model.Face(1, clip, a.T, dt);
        _model.Animate(a);
        _model.Body.SetInstanceShaderParameter("glow_boost", c.Freed ? Math.Min(1f, c.FreedT) : 0.05f);
        _model.UpdatePivot(Vector2.One, 0f, -_footY, false);
        _model.Position = new Vector3(0, -_footY, 0);
        // a spark of charge at his joints now and then, and the flare as he wakes
        float flick = MathF.Max(0f, MathF.Sin(Time * 7.3f) * MathF.Sin(Time * 2.1f));
        _light.LightEnergy = c.Freed ? Math.Max(0f, 2.2f - c.FreedT * 1.4f) : 0.25f * flick;
    }
}

/// <summary>The power cell: a glass cylinder capped in brass, a pale blue charge crackling inside it, lighting the floor round it.</summary>
public partial class PowerCellView : PropView
{
    private MeshInstance3D _core;
    private OmniLight3D _light;
    private static readonly Color Charge = new(0.55f, 0.85f, 1f);

    protected override void Build()
    {
        var brass = new Color(0.58f, 0.45f, 0.2f);
        var mb = new MeshBuilder();
        mb.Tube(new[] { new Vector3(0, 0.0f, 0), new Vector3(0, 0.05f, 0) }, new[] { 0.085f, 0.085f }, 12, brass);
        mb.Tube(new[] { new Vector3(0, 0.33f, 0), new Vector3(0, 0.38f, 0) }, new[] { 0.085f, 0.085f }, 12, brass);
        mb.Tube(new[] { new Vector3(0, 0.38f, 0), new Vector3(0, 0.43f, 0) }, new[] { 0.03f, 0.02f }, 8, brass);
        foreach (float a in new[] { 0f, 2.1f, 4.2f })
            mb.Tube(new[] { new Vector3(MathF.Cos(a) * 0.08f, 0.05f, MathF.Sin(a) * 0.08f), new Vector3(MathF.Cos(a) * 0.08f, 0.33f, MathF.Sin(a) * 0.08f) }, new[] { 0.01f, 0.01f }, 5, brass);
        AddChild(PropViews.Mesh(mb, PropViews.VertexColored));
        var glass = new MeshBuilder();
        glass.Tube(new[] { new Vector3(0, 0.05f, 0), new Vector3(0, 0.33f, 0) }, new[] { 0.072f, 0.072f }, 12, Colors.White);
        _core = PropViews.Mesh(glass, PropViews.Emissive(Charge, 1.6f, 0.7f), false);
        AddChild(_core);
        _light = PropViews.Light(Charge, 0.7f, 3f);
        _light.Position = new Vector3(0, 0.25f, 0.3f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (PowerCell)Owner2D;
        Follow(default, 0f);
        Rotation = new Vector3(0, c.T * 0.6f, 0);
        float crackle = 0.75f + 0.25f * MathF.Sin(c.T * 31f) * MathF.Sin(c.T * 7f);
        _light.LightEnergy = 0.7f * crackle;
        _core.Scale = new Vector3(1f, 1f, 1f) * (0.96f + 0.04f * crackle);
    }
}
