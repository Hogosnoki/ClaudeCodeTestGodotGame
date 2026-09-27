using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// An afterimage: the creature's pose at this instant, left behind as a glowing shell that
/// fades (dodges, air dashes, finishers). It copies the skeleton's pose once and wears the
/// same skinned mesh with an additive rim-light material.
/// </summary>
public partial class Ghost3D : Node3D
{
    private static ShaderMaterial _mat;
    private MeshInstance3D _mesh;
    private float _life, _max;
    private Color _tint;

    public static void ReleaseShared() { _mat?.Dispose(); _mat = null; }

    public static void Spawn(CreatureModel src, Color tint, float life)
    {
        var stage = Stage3D.I;
        if (stage == null || src == null || !IsInstanceValid(src) || !src.IsInsideTree()) return;
        _mat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_ghost.gdshader") };
        var g = new Ghost3D { _life = life, _max = life, _tint = tint };
        stage.AddChild(g);
        g.GlobalTransform = src.Skel.GlobalTransform;
        var skel = new Skeleton3D();
        g.AddChild(skel);
        var from = src.Skel;
        int n = from.GetBoneCount();
        for (int k = 0; k < n; k++)
        {
            skel.AddBone(from.GetBoneName(k));
            skel.SetBoneParent(k, from.GetBoneParent(k));
            skel.SetBoneRest(k, from.GetBoneRest(k));
        }
        for (int k = 0; k < n; k++)
        {
            skel.SetBonePosePosition(k, from.GetBonePosePosition(k));
            skel.SetBonePoseRotation(k, from.GetBonePoseRotation(k));
            skel.SetBonePoseScale(k, from.GetBonePoseScale(k));
        }
        g._mesh = new MeshInstance3D
        {
            Mesh = src.Body.Mesh, Skin = src.Body.Skin, MaterialOverride = _mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        skel.AddChild(g._mesh);
        g._mesh.Skeleton = new NodePath("..");
        g.Apply();
    }

    private void Apply()
    {
        float a = Math.Clamp(_life / _max, 0f, 1f);
        var c = new Color(_tint.R, _tint.G, _tint.B).SrgbToLinear();
        _mesh.SetInstanceShaderParameter("ghost_color", new Color(c.R, c.G, c.B, a * 0.9f));
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0) { QueueFree(); return; }
        Apply();
    }
}
