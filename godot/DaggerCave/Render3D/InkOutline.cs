using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The ink line round each creature, drawn as a screen-space edge pass (the Sobel of the screen's creatures).
/// Add one to any 3D view, given that view's camera. It keeps a small viewport beside it whose camera
/// follows the view's camera and sees only the creatures' bodies (their visual layer <see cref="Layer"/>);
/// the creature shader answers that camera with a flat mask of distance and ink colour instead of its
/// usual look. A full-screen pass in the view (ink_edge.gdshader) then paints the pixels just outside
/// the mask. Nothing of a creature is drawn twice in the view itself: there is no second body, hull or
/// silhouette, only the line, and none over the creature.
/// </summary>
public partial class InkOutline : Node3D
{
    /// <summary>The visual layer of the meshes the line goes round (a creature's body).</summary>
    public const uint Layer = 1u << 19;

    private static readonly List<InkOutline> All = new();
    private static Shader _shader;

    /// <summary>Turns the lines on or off in every view (the graphics setting).</summary>
    public static void SetAll(bool on)
    {
        foreach (var i in All) if (GodotObject.IsInstanceValid(i)) i.Enabled = on;
    }

    private readonly Camera3D _main;
    private SubViewport _vp;
    private Camera3D _cam;
    private MeshInstance3D _quad;
    private bool _enabled = GameSettings.InkOutlines;
    private bool _active = true;

    public InkOutline() { }
    public InkOutline(Camera3D main) { _main = main; }

    /// <summary>The line shows (the graphics setting).</summary>
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; Apply(); }
    }

    /// <summary>The view is being rendered (a view that is switched off needn't make its mask).</summary>
    public bool Active
    {
        get => _active;
        set { _active = value; Apply(); }
    }

    public override void _EnterTree() { if (!All.Contains(this)) All.Add(this); }
    public override void _ExitTree() => All.Remove(this);

    public override void _Ready()
    {
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/ink_edge.gdshader");
        _vp = new SubViewport
        {
            Name = "InkMask", OwnWorld3D = false, TransparentBg = true, UseHdr2D = true, Msaa3D = Viewport.Msaa.Disabled,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Size = new Vector2I(64, 64),
            // (the mask is read by the pass, never drawn to a screen: nothing to filter)
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
        };
        AddChild(_vp);
        // flat black, nothing added by the environment: the creature shader's answer is read back as it is
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.Black, AmbientLightEnergy = 0f,
            TonemapMode = Godot.Environment.ToneMapper.Linear, TonemapExposure = 1f,
        };
        _cam = new Camera3D { CullMask = Layer, Environment = env, Current = true };
        _vp.AddChild(_cam);

        var mat = new ShaderMaterial { Shader = _shader };
        mat.SetShaderParameter("mask_tex", _vp.GetTexture());
        mat.RenderPriority = 100;
        _quad = new MeshInstance3D
        {
            Name = "InkEdge", Mesh = new QuadMesh { Size = new Vector2(2, 2) }, MaterialOverride = mat,
            ExtraCullMargin = 16384f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = 1,
        };
        AddChild(_quad);
        Apply();
        Follow();
    }

    private void Apply()
    {
        if (_quad == null) return;
        bool on = _enabled && _active;
        _quad.Visible = on;
        _vp.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }

    public override void _Process(double delta)
    {
        if (_vp == null) return;
        // a view that was switched off from outside (a portrait nobody is looking at) makes no mask
        if (GetViewport() is SubViewport sv) Active = sv.RenderTargetUpdateMode != SubViewport.UpdateMode.Disabled;
        Follow();
    }

    /// <summary>The mask is the same size as the view's own 3D image, and seen from the same camera.</summary>
    private void Follow()
    {
        if (_main == null || !GodotObject.IsInstanceValid(_main) || _cam == null) return;
        var host = GetViewport();
        var size = host is SubViewport sv ? sv.Size : GetWindow().Size;
        float scale = host.Scaling3DScale;
        var want = new Vector2I(Math.Max(16, (int)(size.X * scale)), Math.Max(16, (int)(size.Y * scale)));
        if (_vp.Size != want) _vp.Size = want;
        _cam.GlobalTransform = _main.GlobalTransform;
        _cam.Projection = _main.Projection;
        _cam.Fov = _main.Fov;
        _cam.Size = _main.Size;
        _cam.Near = _main.Near;
        _cam.Far = _main.Far;
        _cam.KeepAspect = _main.KeepAspect;
        _cam.HOffset = _main.HOffset;
        _cam.VOffset = _main.VOffset;
    }
}
