using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Reusable anatomy for creature designs: jointed legs, gaits, teeth, eye clusters.</summary>
public static class DesignKit
{
    /// <summary>An arthropod leg: its bones (femur, tibia, tarsus) and rest joints.</summary>
    public sealed class Leg
    {
        public int Femur, Tibia, Tarsus;
        public Vector3 Hip, Knee, Ankle, Tip;
        /// <summary>Outward direction on the ground plane.</summary>
        public Vector3 Out;
        public float Phase;
        public int Side; // +1 right (+Z), -1 left
    }

    /// <summary>
    /// Builds an arthropod leg from the hip outward: up to a high knee, down to the ankle and a
    /// pointed tip on the ground. <paramref name="yawDeg"/> turns it from straight out (0) toward
    /// the front (positive).
    /// </summary>
    public static Leg ArthroLeg(Sculptor s, string name, int parent, Vector3 hip, float yawDeg, int side, float reach, float kneeH, float floorY,
        float r0, Color col, Mat mat = Mat.Chitin, float bristles = 0f, Color? tipCol = null)
    {
        float yaw = Mathf.DegToRad(yawDeg);
        var outDir = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw) * side).Normalized();
        var knee = hip + outDir * reach * 0.38f + Vector3.Up * kneeH;
        var ankle = hip + outDir * reach * 0.78f + Vector3.Up * (kneeH * 0.35f);
        var tip = new Vector3(hip.X, floorY, hip.Z) + outDir * reach;
        var leg = new Leg { Hip = hip, Knee = knee, Ankle = ankle, Tip = tip, Out = outDir, Side = side };
        leg.Femur = s.Bone(name + "_f", parent, hip);
        leg.Tibia = s.Bone(name + "_t", leg.Femur, knee);
        leg.Tarsus = s.Bone(name + "_s", leg.Tibia, ankle);
        s.Limb(leg.Femur, hip, knee, r0, r0 * 0.8f, col, mat, 0.012f);
        s.Ball(leg.Tibia, knee, r0 * 0.85f, col, mat, 0.008f);
        s.Limb(leg.Tibia, knee, ankle, r0 * 0.78f, r0 * 0.55f, col, mat, 0.008f);
        s.Ball(leg.Tarsus, ankle, r0 * 0.58f, col, mat, 0.006f);
        s.Limb(leg.Tarsus, ankle, tip + (ankle - tip) * 0.08f, r0 * 0.52f, r0 * 0.22f, col, mat, 0.006f);
        s.Horn(leg.Tarsus, tip + (ankle - tip) * 0.1f, tip, r0 * 0.25f, tipCol ?? col.Darkened(0.3f), Mat.Claw, sides: 5, rings: 3);
        if (bristles > 0f)
        {
            var rng = s.Rng;
            for (int k = 0; k < (int)(6 * bristles); k++)
            {
                bool femur = k % 2 == 0;
                var a = femur ? hip : knee; var b = femur ? knee : ankle;
                float t = 0.2f + (float)rng.NextDouble() * 0.7f;
                var p = a.Lerp(b, t);
                var dir = (outDir * 0.3f + Vector3.Up * 0.6f + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.6f).Normalized();
                s.Horn(femur ? leg.Femur : leg.Tibia, p, p + dir * r0 * 1.6f, r0 * 0.14f, col.Darkened(0.2f), Mat.Claw, sides: 4, rings: 2);
            }
        }
        return leg;
    }

    /// <summary>
    /// Poses an arthropod leg for a gait phase (0..1; the first half is the step through the air).
    /// <paramref name="stride"/> is the forward/back swing (degrees), <paramref name="lift"/> how far it rises.
    /// </summary>
    public static void Step(CreaturePose p, Leg leg, float phase, float stride, float lift, float crouch = 0f)
    {
        float ph = ((phase % 1f) + 1f) % 1f;
        float swing = ph < 0.5f ? Mathf.Lerp(-1f, 1f, W3.Smooth01(ph * 2f)) : Mathf.Lerp(1f, -1f, (ph - 0.5f) * 2f);
        float up = ph < 0.5f ? MathF.Sin(ph * 2f * MathF.PI) : 0f;
        // yaw about the vertical: +Y turns a right-side leg forward and a left-side leg back
        p.AddAxis(leg.Femur, Vector3.Up, swing * stride * leg.Side);
        // lift about the horizontal axis across the leg (a negative turn raises it on either side)
        var across = Vector3.Up.Cross(leg.Out).Normalized();
        p.AddAxis(leg.Femur, across, -(up * lift + crouch));
        p.AddAxis(leg.Tibia, across, up * lift * 0.6f + crouch * 1.4f);
    }

    /// <summary>Folds a leg in toward the body (death curls, drops, grabs); 0 = rest, 1 = fully curled.</summary>
    public static void Curl(CreaturePose p, Leg leg, float amount)
    {
        var across = Vector3.Up.Cross(leg.Out).Normalized();
        p.AddAxis(leg.Femur, across, -amount * 35f);
        p.AddAxis(leg.Tibia, across, amount * 75f);
        p.AddAxis(leg.Tarsus, across, amount * 60f);
    }

    /// <summary>A pointed hexagonal crystal from <paramref name="at"/> along <paramref name="dir"/>.</summary>
    public static void CrystalAt(MeshBuilder mb, Vector3 at, Vector3 dir, float radius, float length, Color col, float twist = 0f)
    {
        var c = new MeshBuilder();
        c.Crystal(radius, length, Math.Min(length * 0.35f, radius * 2.2f), col, twist);
        var y = dir.Normalized();
        var x = Math.Abs(y.Y) < 0.95f ? y.Cross(Vector3.Up).Normalized() : Vector3.Right;
        var z = x.Cross(y);
        mb.Append(c, new Transform3D(new Basis(x, y, z), at));
    }

    /// <summary>A row of teeth along a jaw line from a to b, pointing along dir (fangs get longer toward the front).</summary>
    public static void Teeth(Sculptor s, int bone, Vector3 a, Vector3 b, Vector3 dir, int count, float len, float r, Color col, float frontBoost = 0.6f, float jitter = 0.25f)
    {
        var rng = s.Rng;
        for (int k = 0; k < count; k++)
        {
            float t = count == 1 ? 0.5f : k / (float)(count - 1);
            var p = a.Lerp(b, t);
            float l = len * (1f + frontBoost * t) * (0.75f + (float)rng.NextDouble() * jitter * 2f);
            var d = (dir + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.25f).Normalized();
            s.Horn(bone, p, p + d * l, r * (1f + frontBoost * 0.5f * t), col, Mat.Bone, sides: 5, rings: 3);
        }
    }
}
