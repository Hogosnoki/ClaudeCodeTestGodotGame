using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>A creature type built once: its sculpted mesh, skin, skeleton layout and shared material.</summary>
public sealed class CreatureKit
{
    public CreatureDesign Design;
    public SculptResult Sculpt;
    public ShaderMaterial Material;
    public float BuildMs;
}

/// <summary>Builds each creature type on first use and keeps it.</summary>
public static class CreatureLibrary
{
    private static readonly Dictionary<string, CreatureKit> Kits = new();
    private static readonly Dictionary<string, Func<CreatureDesign>> Designs = new();
    private static Shader _shader;

    public static void Register(string name, Func<CreatureDesign> make) => Designs[name] = make;
    public static bool Has(string name) { EnsureRegistered(); return Designs.ContainsKey(name); }

    private static bool _registered;
    private static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        CreatureRegistry.RegisterAll();
    }

    public static CreatureKit Get(string name)
    {
        EnsureRegistered();
        if (Kits.TryGetValue(name, out var kit)) return kit;
        if (!Designs.TryGetValue(name, out var make)) return null;
        ulong t0 = Time.GetTicksMsec();
        var design = make();
        var s = new Sculptor(design.Seed) { Cell = design.Cell };
        design.Sculpt(s);
        var res = SculptMesher.Build(s);
        design.BindBones(res.Bones);
        _shader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/creature.gdshader");
        var mat = new ShaderMaterial { Shader = _shader };
        var look = design.Look;
        mat.SetShaderParameter("eye_color", look.Eye);
        mat.SetShaderParameter("eye_energy", look.EyeEnergy);
        mat.SetShaderParameter("glow_color", look.Glow);
        mat.SetShaderParameter("glow_energy", look.GlowEnergy);
        mat.SetShaderParameter("rim_color", look.Rim);
        mat.SetShaderParameter("rim_energy", look.RimEnergy);
        mat.SetShaderParameter("detail_scale", look.DetailScale);
        mat.SetShaderParameter("detail_strength", look.DetailStrength);
        mat.SetShaderParameter("veins", look.Veins);
        mat.SetShaderParameter("vein_color", look.VeinColor);
        mat.SetShaderParameter("wet", look.Wet);
        res.Mesh.SurfaceSetMaterial(0, mat);
        kit = new CreatureKit { Design = design, Sculpt = res, Material = mat, BuildMs = Time.GetTicksMsec() - t0 };
        Kits[name] = kit;
        GD.Print($"[3D] creature '{name}': {res.Triangles / 1000f:0.0}k triangles, {res.Bones.Length} bones, built in {kit.BuildMs} ms");
        return kit;
    }
}

/// <summary>
/// One creature on screen: a skeleton posed every frame by its design, wearing the shared
/// sculpted mesh. Facing turns the whole body (through a front view on turn clips), and the 2D
/// animator's squash-and-stretch, hit punch, roll, flash, tint and fade carry over.
/// </summary>
public partial class CreatureModel : Node3D
{
    public CreatureKit Kit { get; private set; }
    public CreatureDesign Design => Kit.Design;
    public Skeleton3D Skel { get; private set; }
    public MeshInstance3D Body { get; private set; }
    public Node3D Pivot { get; private set; }
    public CreaturePose Pose { get; private set; }
    public OmniLight3D Light { get; private set; }

    private float _yaw = float.NaN;
    private Quaternion[] _prevRot;
    private string _lastClip = "";
    private float _blendT = 1f;

    public static CreatureModel Create(string name)
    {
        var kit = CreatureLibrary.Get(name);
        if (kit == null) return null;
        var m = new CreatureModel { Name = "Creature_" + name };
        m.Init(kit);
        return m;
    }

    private void Init(CreatureKit kit)
    {
        Kit = kit;
        Pivot = new Node3D { Name = "Pivot" };
        AddChild(Pivot);
        Skel = new Skeleton3D { Name = "Skeleton" };
        Pivot.AddChild(Skel);
        var bones = kit.Sculpt.Bones;
        for (int k = 0; k < bones.Length; k++)
        {
            Skel.AddBone(bones[k].Name);
            if (bones[k].Parent >= 0) Skel.SetBoneParent(k, bones[k].Parent);
            var local = bones[k].Head - (bones[k].Parent >= 0 ? bones[bones[k].Parent].Head : Vector3.Zero);
            Skel.SetBoneRest(k, new Transform3D(Basis.Identity, local));
        }
        Skel.ResetBonePoses();
        Body = new MeshInstance3D { Name = "Body", Mesh = kit.Sculpt.Mesh, Skin = kit.Sculpt.Skin };
        Skel.AddChild(Body);
        Body.Skeleton = new NodePath("..");
        // per-instance shader state starts neutral (never rely on uniform defaults here)
        SetFlash(0f, Colors.White);
        SetFade(1f);
        SetDissolve(0f);
        SetTint(Colors.White, 0f);
        Pose = new CreaturePose(bones.Length);
        _prevRot = new Quaternion[bones.Length];
        for (int k = 0; k < bones.Length; k++) _prevRot[k] = Quaternion.Identity;
        var look = kit.Design.Look;
        if (look.LightEnergy > 0f)
        {
            Light = new OmniLight3D { LightColor = look.LightColor, LightEnergy = look.LightEnergy, OmniRange = look.LightRange, Position = look.LightOffset, ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f };
            Pivot.AddChild(Light);
        }
        kit.Design.Attach(this);
    }

    /// <summary>Bone attachment (weapons, lanterns): a node that follows the bone.</summary>
    public BoneAttachment3D AttachTo(string bone)
    {
        var at = new BoneAttachment3D { BoneName = bone };
        Skel.AddChild(at);
        return at;
    }

    /// <summary>Runs the design's animation for this frame and applies it.</summary>
    public void Animate(in AnimInput a)
    {
        var pose = Pose;
        pose.Clear();
        Design.Animate(pose, a);
        // cross-fade briefly whenever the clip changes so poses never snap
        if (a.Clip != _lastClip) { _lastClip = a.Clip; _blendT = 0f; }
        _blendT = Math.Min(1f, _blendT + a.Dt / 0.09f);
        float w = _blendT * _blendT * (3f - 2f * _blendT);
        var bones = Kit.Sculpt.Bones;
        for (int k = 0; k < bones.Length; k++)
        {
            var q = w >= 1f ? pose.Rot[k] : _prevRot[k].Slerp(pose.Rot[k], w);
            q = q.Normalized();
            _prevRot[k] = q;
            Skel.SetBonePoseRotation(k, q);
            var local = bones[k].Head - (bones[k].Parent >= 0 ? bones[bones[k].Parent].Head : Vector3.Zero);
            Skel.SetBonePosePosition(k, local + pose.Offset[k]);
        }
    }

    /// <summary>Facing yaw: right = toward the camera by the design's three-quarter angle; turn clips sweep through the front.</summary>
    public void Face(int facing, string clip, float t, float dt)
    {
        float tq = Design.ThreeQuarter;
        float right = -tq, left = tq - 180f;
        float target = facing >= 0 ? right : left;
        if (clip == "turn_r2l") _yaw = Mathf.Lerp(right, left, Ease(t));
        else if (clip == "turn_l2r") _yaw = Mathf.Lerp(left, right, Ease(t));
        else if (float.IsNaN(_yaw)) _yaw = target;
        else _yaw = Mathf.Lerp(_yaw, target, 1f - MathF.Exp(-dt * 22f));
    }

    /// <summary>
    /// Places the body: squash-and-stretch in screen axes anchored at the feet (<paramref name="footY"/>
    /// metres below the origin), the 2D roll about the view axis, the facing yaw, and an upside-down
    /// flip for ceiling crawlers.
    /// </summary>
    public void UpdatePivot(Vector2 squash, float roll, float footY, bool flipV)
    {
        float sz = MathF.Sqrt(Math.Max(0.01f, squash.X * squash.Y));
        var yaw = new Basis(Vector3.Up, Mathf.DegToRad(_yaw + Pose.Yaw));
        if (flipV) yaw = yaw * new Basis(Vector3.Right, MathF.PI);
        var b = new Basis(Vector3.Back, roll) * Basis.FromScale(new Vector3(squash.X, squash.Y, sz)) * yaw;
        // keep the feet planted while squashing
        var anchor = new Vector3(0, footY * (squash.Y - 1f), 0);
        Pivot.Transform = new Transform3D(b, anchor + Pose.Root);
    }

    private static float Ease(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    // ---- shader state (per instance, no material copies)
    public void SetFlash(float amount, Color col)
    {
        Body.SetInstanceShaderParameter("flash", amount);
        Body.SetInstanceShaderParameter("flash_color", col);
    }

    public void SetTint(Color tint, float strength) => Body.SetInstanceShaderParameter("tint", new Color(tint.R, tint.G, tint.B, strength));
    public void SetFade(float visible) => Body.SetInstanceShaderParameter("fade", visible);
    public void SetDissolve(float amount) => Body.SetInstanceShaderParameter("dissolve", amount);
}
