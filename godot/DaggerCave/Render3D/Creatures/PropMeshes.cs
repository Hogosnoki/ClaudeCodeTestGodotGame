using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Hand-held and worn props built in code: swords, a kite shield, a lantern. Swords are built in
/// "grip space": the fist at the origin, the blade running down -Y (continuing the arm when it
/// hangs), its flat facing ±Z (the camera) so a swing shows the whole blade.
/// </summary>
public static class PropMeshes
{
    /// <summary>A box from its centre and half extents (flat shaded).</summary>
    public static void Box(MeshBuilder mb, Vector3 c, Vector3 h, Color col, Basis? rot = null)
    {
        var r = rot ?? Basis.Identity;
        Vector3[] n = { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward };
        foreach (var f in n)
        {
            var u = MathF.Abs(f.Y) > 0.5f ? Vector3.Right : Vector3.Up;
            var v = f.Cross(u);
            var fc = c + r * (f * new Vector3(h.X, h.Y, h.Z) * f.Abs().Sign());
            var fu = r * (u * new Vector3(h.X, h.Y, h.Z));
            var fv = r * (v * new Vector3(h.X, h.Y, h.Z));
            var nn = r * f;
            int a = mb.Add(fc - fu - fv, nn, col), b = mb.Add(fc + fu - fv, nn, col), cc = mb.Add(fc + fu + fv, nn, col), d = mb.Add(fc - fu + fv, nn, col);
            mb.Quad(a, b, cc, d);
        }
        mb.FixWinding(mb.I.Count - 36, mb.I.Count);
    }

    /// <summary>
    /// A sword: pommel, wrapped grip, crossguard and a diamond-section blade with a fuller.
    /// Vertex colour alpha marks the blade (1) for a colder sheen.
    /// </summary>
    public static MeshBuilder Sword(float blade, float width, Color steel, Color guard, Color grip, float guardSpan = 0.1f)
    {
        var mb = new MeshBuilder();
        // pommel and grip
        DecorMeshes.AddSphere(mb, new Vector3(0, 0.115f, 0), 0.022f, guard, 5);
        mb.Tube(new[] { new Vector3(0, 0.1f, 0), new Vector3(0, -0.01f, 0) }, new[] { 0.016f, 0.017f }, 8, grip, capStart: true);
        // crossguard, slightly swept
        Box(mb, new Vector3(0, -0.015f, 0), new Vector3(guardSpan, 0.011f, 0.013f), guard);
        DecorMeshes.AddSphere(mb, new Vector3(guardSpan, -0.02f, 0), 0.015f, guard, 4);
        DecorMeshes.AddSphere(mb, new Vector3(-guardSpan, -0.02f, 0), 0.015f, guard, 4);
        // blade: rows of diamond cross-sections tapering to a point
        int rows = 12;
        float thick = width * 0.16f;
        int start = mb.Count;
        for (int i = 0; i <= rows; i++)
        {
            float t = i / (float)rows;
            float y = -0.03f - blade * t;
            float w = width * (t < 0.8f ? 1f - 0.25f * t : (1f - 0.2f) * (1f - (t - 0.8f) / 0.2f));
            float th = thick * (1f - 0.5f * t) + 0.001f;
            var col = new Color(steel, 1f);
            // edge (+X), flat (+Z), back edge (-X), flat (-Z)
            mb.Add(new Vector3(w, y, 0), Vector3.Right, col);
            mb.Add(new Vector3(0, y, th), Vector3.Back, col);
            mb.Add(new Vector3(-w, y, 0), Vector3.Left, col);
            mb.Add(new Vector3(0, y, -th), Vector3.Forward, col);
        }
        for (int i = 0; i < rows; i++)
            for (int k = 0; k < 4; k++)
            {
                // rows run down the blade and k runs +X -> +Z -> -X -> -Z, so (a, b, c) faces outward
                int a = start + i * 4 + k, b = start + i * 4 + (k + 1) % 4, c = a + 4, d = b + 4;
                mb.Tri(a, b, c); mb.Tri(b, d, c);
            }
        mb.SmoothNormals();
        return mb;
    }

    /// <summary>
    /// A longsword in the same frame as <see cref="Sword"/> (grip at the origin, blade running down -Y,
    /// the guard at y -0.015): a faceted pommel, a wrapped grip, a plain straight crossguard with
    /// squared ends, and a broad blade with a fuller down each face.
    /// </summary>
    public static MeshBuilder Longsword(float blade, float width, Color steel, Color guard, Color grip)
    {
        var mb = new MeshBuilder();
        // a faceted pommel (few sides, so it catches the light in planes)
        DecorMeshes.AddSphere(mb, new Vector3(0, 0.122f, 0), 0.028f, guard, 3);
        Box(mb, new Vector3(0, 0.104f, 0), new Vector3(0.02f, 0.006f, 0.02f), guard);
        // the grip, wrapped in leather: bands over a shaft
        mb.Tube(new[] { new Vector3(0, 0.1f, 0), new Vector3(0, -0.01f, 0) }, new[] { 0.016f, 0.017f }, 8, grip, capStart: true);
        for (int k = 0; k < 6; k++)
        {
            float y = 0.09f - k * 0.018f;
            mb.Tube(new[] { new Vector3(0, y + 0.004f, 0), new Vector3(0, y - 0.004f, 0) }, new[] { 0.0195f, 0.0195f }, 8, grip.Darkened(0.35f), capStart: true);
        }
        // the crossguard: a plain bar, squared at its ends, a little collar under it
        Box(mb, new Vector3(0, -0.015f, 0), new Vector3(0.115f, 0.012f, 0.014f), guard);
        Box(mb, new Vector3(0.115f, -0.02f, 0), new Vector3(0.008f, 0.02f, 0.017f), guard);
        Box(mb, new Vector3(-0.115f, -0.02f, 0), new Vector3(0.008f, 0.02f, 0.017f), guard);
        Box(mb, new Vector3(0, -0.03f, 0), new Vector3(0.026f, 0.012f, 0.02f), guard);
        // the blade: broad, tapering to a point
        int rows = 14;
        float thick = width * 0.2f;
        int start = mb.Count;
        for (int i = 0; i <= rows; i++)
        {
            float t = i / (float)rows;
            float y = -0.04f - blade * t;
            float w = width * (t < 0.82f ? 1f - 0.2f * t : (1f - 0.164f) * (1f - (t - 0.82f) / 0.18f));
            float th = thick * (1f - 0.55f * t) + 0.001f;
            var col = new Color(steel, 1f);
            mb.Add(new Vector3(w, y, 0), Vector3.Right, col);
            mb.Add(new Vector3(0, y, th), Vector3.Back, col);
            mb.Add(new Vector3(-w, y, 0), Vector3.Left, col);
            mb.Add(new Vector3(0, y, -th), Vector3.Forward, col);
        }
        for (int i = 0; i < rows; i++)
            for (int k = 0; k < 4; k++)
            {
                int a = start + i * 4 + k, b = start + i * 4 + (k + 1) % 4, c = a + 4, d = b + 4;
                mb.Tri(a, b, c); mb.Tri(b, d, c);
            }
        mb.SmoothNormals();
        // a fuller down each face (a darker groove)
        var groove = steel.Darkened(0.35f);
        for (int side = -1; side <= 1; side += 2)
            Box(mb, new Vector3(0, -0.04f - blade * 0.38f, side * (thick * 0.72f)), new Vector3(width * 0.16f, blade * 0.33f, 0.0016f), groove);
        return mb;
    }

    /// <summary>A kite shield facing +X, centred on the origin: rim, boss and a painted field.</summary>
    public static MeshBuilder KiteShield(float height, float width, Color field, Color rim, Color emblem)
    {
        var mb = new MeshBuilder();
        // outline in the Y-Z plane: rounded top, point at the bottom
        var outline = new List<Vector2>();
        int n = 28;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n;
            float ang = MathF.PI * t;
            if (t <= 0.5f)
            {
                // top arc
                outline.Add(new Vector2(MathF.Cos(ang) * width * 0.5f, height * 0.18f + MathF.Sin(ang) * height * 0.12f));
            }
        }
        // sides tapering to the point
        for (int k = 1; k <= 10; k++)
        {
            float t = k / 10f;
            outline.Add(new Vector2(-width * 0.5f * (1f - t * t * 0.92f) * (1f - t * 0.08f), height * 0.18f - t * height * 0.88f));
        }
        var right = new List<Vector2>();
        for (int k = outline.Count - 2; k >= 0; k--) right.Add(new Vector2(-outline[k].X, outline[k].Y));
        // build the full loop: top arc (right to left), left side down, point, right side up
        var loop = new List<Vector2>();
        for (int k = 0; k <= n / 2; k++) loop.Add(outline[k]);
        for (int k = n / 2 + 1; k < outline.Count; k++) loop.Add(outline[k]);
        for (int k = outline.Count - 2; k > n / 2; k--) loop.Add(new Vector2(-outline[k].X, outline[k].Y));
        float bulge = width * 0.12f;
        float thick = 0.018f;
        // front face (+X), slightly convex
        int c0 = mb.Add(new Vector3(bulge + thick, 0, 0), Vector3.Right, field);
        int f0 = mb.Count;
        foreach (var q in loop)
        {
            float r = q.Length() / (width * 0.5f);
            float bz = bulge * (1f - Math.Min(1f, (q.X * q.X) / (width * width * 0.25f)));
            var pcol = MathF.Abs(q.X) < width * 0.07f || MathF.Abs(q.Y - height * 0.02f) < height * 0.035f ? emblem : field;
            mb.Add(new Vector3(bz + thick, q.Y, q.X), new Vector3(1f, 0, -q.X / width).Normalized(), pcol);
        }
        for (int k = 0; k < loop.Count; k++) mb.Tri(c0, f0 + k, f0 + (k + 1) % loop.Count);
        // back face
        int c1 = mb.Add(new Vector3(bulge * 0.6f, 0, 0), Vector3.Left, rim.Darkened(0.4f));
        int b0 = mb.Count;
        foreach (var q in loop)
        {
            float bz = bulge * (1f - Math.Min(1f, (q.X * q.X) / (width * width * 0.25f)));
            mb.Add(new Vector3(bz - thick * 0.5f, q.Y, q.X), Vector3.Left, rim.Darkened(0.4f));
        }
        for (int k = 0; k < loop.Count; k++) mb.Tri(c1, b0 + (k + 1) % loop.Count, b0 + k);
        // rim band around the edge
        for (int k = 0; k < loop.Count; k++)
        {
            int k2 = (k + 1) % loop.Count;
            var p0 = mb.V[f0 + k]; var p1 = mb.V[f0 + k2]; var q0 = mb.V[b0 + k]; var q1 = mb.V[b0 + k2];
            var nrm = new Vector3(0, loop[k].Y, loop[k].X).Normalized();
            int a = mb.Add(p0, nrm, rim), b = mb.Add(p1, nrm, rim), cc = mb.Add(q1, nrm, rim), d = mb.Add(q0, nrm, rim);
            mb.Quad(a, b, cc, d);
        }
        // boss
        DecorMeshes.AddSphere(mb, new Vector3(bulge + thick + 0.005f, height * 0.02f, 0), width * 0.09f, rim, 5);
        mb.FixWinding(0, mb.I.Count);
        return mb;
    }

    /// <summary>A small caged lantern hanging from a ring at the origin; the glass (alpha 1) glows.</summary>
    public static MeshBuilder Lantern(Color frame, Color glass)
    {
        var mb = new MeshBuilder();
        var g = new Color(glass, 1f);
        Box(mb, new Vector3(0, -0.075f, 0), new Vector3(0.026f, 0.036f, 0.026f), g);
        Box(mb, new Vector3(0, -0.035f, 0), new Vector3(0.036f, 0.006f, 0.036f), frame);
        Box(mb, new Vector3(0, -0.115f, 0), new Vector3(0.036f, 0.007f, 0.036f), frame);
        for (int k = 0; k < 4; k++)
        {
            float x = (k & 1) == 0 ? 0.031f : -0.031f, z = (k & 2) == 0 ? 0.031f : -0.031f;
            Box(mb, new Vector3(x, -0.075f, z), new Vector3(0.004f, 0.04f, 0.004f), frame);
        }
        mb.Tube(new[] { new Vector3(0, -0.03f, 0), new Vector3(0, 0.0f, 0) }, new[] { 0.006f, 0.004f }, 5, frame, capStart: false);
        return mb;
    }
}
