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

/// <summary>
/// Builds each creature type once and keeps it. Sculpts are baked on worker threads where
/// possible (<see cref="Prefetch"/> for a level's roster while it loads, <see cref="PrebuildRest"/>
/// in the background afterwards); a type needed before its bake is done is built on the spot.
/// </summary>
public static class CreatureLibrary
{
    private static readonly Dictionary<string, CreatureKit> Kits = new();
    private static readonly Dictionary<string, Func<CreatureDesign>> Designs = new();
    private static readonly Dictionary<string, Lazy<Baked>> Bakes = new();
    private static Shader _shader, _inkShader;

    private sealed class Baked
    {
        public CreatureDesign Design;
        public SculptData Data;
        public float Ms;
    }

    public static void Register(string name, Func<CreatureDesign> make)
    {
        Designs[name] = make;
        Bakes[name] = new Lazy<Baked>(() => Bake(make), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public static bool Has(string name) { EnsureRegistered(); return Designs.ContainsKey(name); }

    private static bool _registered;
    private static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        CreatureRegistry.RegisterAll();
    }

    /// <summary>
    /// Voxel size multiplier for sculpts. In play the camera stands well back, so creatures are
    /// sculpted a little coarser than their designs ask (fewer triangles to skin and to shadow);
    /// look-development sheets set it back to 1.
    /// </summary>
    public static float CellScale = 1.3f;

    private static Baked Bake(Func<CreatureDesign> make)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var design = make();
        // (small creatures with fine limbs keep their full resolution)
        var s = new Sculptor(design.Seed) { Cell = design.Cell * (design.Cell >= 0.014f ? CellScale : 1f) };
        design.Sculpt(s);
        var data = SculptMesher.Bake(s);
        design.BindBones(data.Bones);
        return new Baked { Design = design, Data = data, Ms = (float)sw.Elapsed.TotalMilliseconds };
    }

    /// <summary>Bakes these types in parallel now (blocking), e.g. a level's roster while it loads.</summary>
    public static void Prefetch(IEnumerable<string> names)
    {
        EnsureRegistered();
        var todo = new List<string>();
        foreach (var n in names)
            if (n != null && Bakes.ContainsKey(n) && !Kits.ContainsKey(n) && !todo.Contains(n)) todo.Add(n);
        if (todo.Count == 0) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Threading.Tasks.Parallel.ForEach(todo, n => { _ = Bakes[n].Value; });
        foreach (var n in todo) Get(n);
        GD.Print($"[3D] prefetched {todo.Count} creature types in {sw.ElapsedMilliseconds} ms: {string.Join(", ", todo)}");
    }

    private static System.Threading.Thread _bg;
    private static volatile bool _stopBg;

    /// <summary>Bakes every other type on one low-priority background thread.</summary>
    public static void PrebuildRest()
    {
        EnsureRegistered();
        if (_bg != null) return;
        var lazies = new List<Lazy<Baked>>(Bakes.Values);
        _bg = new System.Threading.Thread(() =>
        {
            foreach (var l in lazies)
            {
                if (_stopBg) return;
                try { _ = l.Value; }
                catch (Exception e) { GD.PrintErr($"[3D] background creature bake failed: {e.Message}"); }
            }
        }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "CreatureBake" };
        _bg.Start();
    }

    /// <summary>Frees every built kit (before the engine shuts down; see PropViews.ReleaseShared).</summary>
    public static void ReleaseShared()
    {
        // stop the background baker first (it finishes the sculpt in hand)
        _stopBg = true;
        _bg?.Join(5000);
        foreach (var k in Kits.Values)
        {
            k.Sculpt.Mesh?.Dispose();
            k.Sculpt.Skin?.Dispose();
            k.Material?.Dispose();
        }
        Kits.Clear();
        _shader = null;
        _inkShader = null;
    }

    public static CreatureKit Get(string name)
    {
        EnsureRegistered();
        if (Kits.TryGetValue(name, out var kit)) return kit;
        if (!Bakes.TryGetValue(name, out var lazy)) return null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool ready = lazy.IsValueCreated;
        var baked = lazy.Value;
        var design = baked.Design;
        var res = SculptMesher.Finish(baked.Data);
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
        if (!float.IsNaN(look.GhostBelow)) { mat.SetShaderParameter("ghost_below", look.GhostBelow); mat.SetShaderParameter("ghost_fade", look.GhostFade); }
        // ink outlines: a Sobel pass over each creature's own pixels (see creature_ink.gdshader)
        _inkShader ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/creature_ink.gdshader");
        var ink = new ShaderMaterial { Shader = _inkShader };
        if (!float.IsNaN(look.GhostBelow)) { ink.SetShaderParameter("ghost_below", look.GhostBelow); ink.SetShaderParameter("ghost_fade", look.GhostFade); }
        mat.NextPass = ink;
        res.Mesh.SurfaceSetMaterial(0, mat);
        kit = new CreatureKit { Design = design, Sculpt = res, Material = mat, BuildMs = baked.Ms };
        Kits[name] = kit;
        GD.Print($"[3D] creature '{name}': {res.Triangles / 1000f:0.0}k triangles, {res.Bones.Length} bones, baked in {baked.Ms:0} ms" +
                 (ready ? " (ahead of time)" : "") + $", finished in {sw.ElapsedMilliseconds} ms");
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
    private float _glow = 1f;
    private Quaternion[] _prevRot;
    private Vector3[] _prevScale;
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
        Body.SetInstanceShaderParameter("glow_boost", 1f);
        Body.SetInstanceShaderParameter("aura", new Color(0, 0, 0, 0));
        Pose = new CreaturePose(bones.Length);
        _prevRot = new Quaternion[bones.Length];
        _prevScale = new Vector3[bones.Length];
        for (int k = 0; k < bones.Length; k++) { _prevRot[k] = Quaternion.Identity; _prevScale[k] = Vector3.One; }
        var look = kit.Design.Look;
        if (look.LightEnergy > 0f)
        {
            Light = new OmniLight3D { LightColor = look.LightColor, LightEnergy = look.LightEnergy, OmniRange = look.LightRange, Position = look.LightOffset, ShadowEnabled = false, LightVolumetricFogEnergy = 0.4f };
            Pivot.AddChild(Light);
        }
        kit.Design.Attach(this);
        SetActorLayer(this);
    }

    /// <summary>Puts every mesh under this node on the actor layer (lit by the actor key and rim).</summary>
    public static void SetActorLayer(Node n)
    {
        if (n is GeometryInstance3D g) g.Layers |= Stage3D.ActorLayer;
        foreach (var c in n.GetChildren()) SetActorLayer(c);
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
        if (pose.Glow != _glow) { _glow = pose.Glow; Body.SetInstanceShaderParameter("glow_boost", _glow); }
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
            var sc = w >= 1f ? pose.Scale[k] : _prevScale[k].Lerp(pose.Scale[k], w);
            _prevScale[k] = sc;
            Skel.SetBonePoseScale(k, sc);
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

    private Color _aura;
    /// <summary>An affliction's glow (weakened, hexed); alpha = strength, 0 = none.</summary>
    public void SetAura(Color c)
    {
        if (c == _aura) return;
        _aura = c;
        Body.SetInstanceShaderParameter("aura", c);
    }
    public void SetDissolve(float amount) => Body.SetInstanceShaderParameter("dissolve", amount);
}
