using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>What a creature's animation reads each frame (all from the 2D simulation).</summary>
public struct AnimInput
{
    /// <summary>The playing clip without its facing suffix ("run", "slash_a_fwd", "turn_r2l"...).</summary>
    public string Clip;
    public int Frame, Frames;
    /// <summary>Progress through the clip, 0..1 (wraps for loops).</summary>
    public float T;
    public bool Loop;
    /// <summary>Seconds of animation time (scaled by the creature's tempo); for idle breathing etc.</summary>
    public float Time;
    public float Dt;
    public int Facing;
    /// <summary>Velocity in metres per second (x right, y up).</summary>
    public Vector2 Vel;
    public bool OnFloor, InWater;
    /// <summary>Seconds since this clip started.</summary>
    public float ClipTime;
    /// <summary>Anything a design wants from its gameplay node (aim, state flags...).</summary>
    public Node2D Owner;
}

/// <summary>
/// Local bone rotations/offsets for one frame. Designs write into it; the model applies it to the
/// skeleton. Angles are degrees; rotations are about the bone's joint in model axes (+X forward,
/// +Y up, +Z the creature's right), applied X, then Y, then Z.
/// </summary>
public sealed class CreaturePose
{
    public readonly Quaternion[] Rot;
    public readonly Vector3[] Offset;
    public readonly Dictionary<int, float> Springs = new();
    public readonly Dictionary<int, float> SpringVel = new();
    /// <summary>Per-creature scratch state for designs (gait phase, last values...).</summary>
    public readonly float[] Vars = new float[16];
    /// <summary>Extra yaw of the whole body (degrees) on top of facing, e.g. to glance at the camera.</summary>
    public float Yaw;
    /// <summary>A whole-body lift (metres), e.g. for hops.</summary>
    public Vector3 Root;

    public CreaturePose(int bones)
    {
        Rot = new Quaternion[bones];
        Offset = new Vector3[bones];
        Clear();
    }

    public void Clear()
    {
        for (int k = 0; k < Rot.Length; k++) { Rot[k] = Quaternion.Identity; Offset[k] = Vector3.Zero; }
        Yaw = 0; Root = Vector3.Zero;
    }

    public static Quaternion Q(float xDeg, float yDeg, float zDeg) =>
        Quaternion.FromEuler(new Vector3(Mathf.DegToRad(xDeg), Mathf.DegToRad(yDeg), Mathf.DegToRad(zDeg)));

    /// <summary>Sets a bone's rotation.</summary>
    public void Set(int bone, float x, float y, float z) { if (bone >= 0) Rot[bone] = Q(x, y, z); }

    /// <summary>Adds a rotation on top of what the bone already has.</summary>
    public void Add(int bone, float x, float y, float z) { if (bone >= 0) Rot[bone] = Rot[bone] * Q(x, y, z); }

    /// <summary>Bends a bone about the side axis (Z): positive lifts a forward-pointing limb.</summary>
    public void Bend(int bone, float deg) => Add(bone, 0, 0, deg);

    public void Move(int bone, Vector3 m) { if (bone >= 0) Offset[bone] += m; }

    /// <summary>
    /// A damped spring toward <paramref name="target"/> kept per creature (tails, ears, capes).
    /// Returns the spring's current value.
    /// </summary>
    public float Spring(int key, float target, float dt, float stiffness = 90f, float damping = 12f)
    {
        if (!Springs.TryGetValue(key, out var x)) { Springs[key] = target; SpringVel[key] = 0; return target; }
        float v = SpringVel[key];
        float left = Math.Min(dt, 0.1f);
        while (left > 0f)
        {
            float h = Math.Min(left, 1f / 120f);
            v += ((target - x) * stiffness - v * damping) * h;
            x += v * h;
            left -= h;
        }
        Springs[key] = x; SpringVel[key] = v;
        return x;
    }
}

/// <summary>The look of a creature type (shared by every instance): glows, rim, detail.</summary>
public sealed class CreatureLook
{
    public Color Eye = new(1f, 0.25f, 0.08f);
    public float EyeEnergy = 7f;
    public Color Glow = new(1f, 0.5f, 0.15f);
    public float GlowEnergy = 3f;
    public Color Rim = new(0.55f, 0.65f, 0.85f);
    public float RimEnergy = 0.35f;
    public float DetailScale = 22f, DetailStrength = 0.5f;
    /// <summary>Dark veins / mottling over the skin (0 = none).</summary>
    public float Veins = 0f;
    public Color VeinColor = new(0.25f, 0.02f, 0.03f);
    /// <summary>Wet sheen on skin (lower roughness).</summary>
    public float Wet = 0f;
    /// <summary>A light the creature carries (eyes that light the ground, a lantern, burning flesh).</summary>
    public Color LightColor;
    public float LightEnergy, LightRange = 3f;
    public Vector3 LightOffset;
}

/// <summary>
/// One creature type: how it is sculpted and how it moves. Built once, instanced many times.
/// </summary>
public abstract class CreatureDesign
{
    /// <summary>The sprite set this design replaces ("bat", "swordsman"...).</summary>
    public abstract string Name { get; }
    public virtual CreatureLook Look => new();
    /// <summary>Voxel size for the sculpt.</summary>
    public virtual float Cell => 0.018f;
    /// <summary>Yaw (degrees) toward the camera while walking/standing, so we see a 3/4 view.</summary>
    public virtual float ThreeQuarter => 22f;
    /// <summary>Where the ground is in model space (the sole of the feet), for previews; NaN = the mesh's lowest point.</summary>
    public virtual float FloorY => float.NaN;
    public virtual int Seed => Name.GetHashCode();

    public abstract void Sculpt(Sculptor s);

    /// <summary>Poses the skeleton for this frame. Bone indices come from <see cref="Sculptor"/> names via <see cref="B"/>.</summary>
    public abstract void Animate(CreaturePose p, in AnimInput a);

    /// <summary>Attachments (weapons, lanterns) once the model exists.</summary>
    public virtual void Attach(CreatureModel m) { }

    private Dictionary<string, int> _bones;
    /// <summary>Bone index by name (-1 when this design has no such bone).</summary>
    public int B(string name) => _bones != null && _bones.TryGetValue(name, out int i) ? i : -1;
    public void BindBones(BoneDef[] bones)
    {
        _bones = new Dictionary<string, int>();
        for (int k = 0; k < bones.Length; k++) _bones[bones[k].Name] = k;
        OnBonesBound();
    }
    /// <summary>Cache bone indices here.</summary>
    protected virtual void OnBonesBound() { }

    // ---- animation helpers

    /// <summary>Piecewise keyframes: (t, value) pairs with smoothstep easing between them.</summary>
    public static float Key(float t, params (float t, float v)[] k)
    {
        if (t <= k[0].t) return k[0].v;
        for (int i = 1; i < k.Length; i++)
            if (t <= k[i].t)
            {
                float u = (t - k[i - 1].t) / Math.Max(1e-5f, k[i].t - k[i - 1].t);
                u = u * u * (3f - 2f * u);
                return k[i - 1].v + (k[i].v - k[i - 1].v) * u;
            }
        return k[^1].v;
    }

    public static float Sin01(float x) => 0.5f + 0.5f * MathF.Sin(x);
    public static float Wave(float t, float cycles = 1f, float phase = 0f) => MathF.Sin((t * cycles + phase) * Mathf.Tau);
}
