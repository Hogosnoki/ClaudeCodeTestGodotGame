using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The bridge between the 2D simulation and the 3D presentation. Gameplay still runs in 2D
/// pixel space (x right, y down); everything you see is drawn in 3D metres (X right, Y up,
/// Z toward the camera), with the play plane at Z = 0. One cave cell (16 px) is one metre.
/// </summary>
public static class W3
{
    /// <summary>Simulation pixels per 3D metre.</summary>
    public const float Ppu = 16f;

    /// <summary>A 2D world point (px) on the 3D play plane, optionally pushed toward (+) or away from (-) the camera.</summary>
    public static Vector3 P(Vector2 px, float z = 0f) => new(px.X / Ppu, -px.Y / Ppu, z);

    /// <summary>A 2D direction or velocity (px) as a 3D vector on the play plane.</summary>
    public static Vector3 D(Vector2 px) => new(px.X / Ppu, -px.Y / Ppu, 0f);

    /// <summary>A length in pixels as metres.</summary>
    public static float M(float px) => px / Ppu;

    /// <summary>A 3D point back in 2D pixel space (Z dropped).</summary>
    public static Vector2 To2(Vector3 p) => new(p.X * Ppu, -p.Y * Ppu);

    /// <summary>A 2D rotation (radians, clockwise on screen because y points down) as a roll about the view axis.</summary>
    public static float Roll(float rot2d) => -rot2d;

    public static float Smooth01(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    public static float SmoothStep(float a, float b, float x) => Smooth01((x - a) / (b - a));

    /// <summary>Polynomial smooth minimum (k = blend width in the same units as a and b).</summary>
    public static float SMin(float a, float b, float k)
    {
        if (k <= 0f) return Math.Min(a, b);
        float h = Math.Max(k - Math.Abs(a - b), 0f) / k;
        return Math.Min(a, b) - h * h * k * 0.25f;
    }

    public static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

    public static Color Lerp(Color a, Color b, float t) => a.Lerp(b, Math.Clamp(t, 0f, 1f));

    /// <summary>Linear-space colour scaled into HDR (for emissive values above 1).</summary>
    public static Color Hdr(Color c, float energy) => new(c.R * energy, c.G * energy, c.B * energy, c.A);
}
