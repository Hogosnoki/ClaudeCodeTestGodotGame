using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Swordsman's axes of ice. The sword is the shaft: through the wind-up of the Ice Axe a crescent bit of ice grows out of each side of
/// the blade near its point, and a crystal spike out of the point itself; the Glacier Cleaver's head is the same grown vast and ornate (the
/// blade first runs on in a long haft of ice, then the great horned bits flare out of its end, crowned and studded with crystals). Each
/// part draws itself out of frost in turn (see <see cref="Player.AxeGrow"/>): its ice carries a noisy gradient out from where it grows (the
/// blade, a crystal's foot), and the frost-grow shader draws only what the growth has reached, the front burning white (fx_frostgrow); it
/// shatters away after the blow. The pose that goes with it is <see cref="AxeHeave"/>.
/// </summary>
public sealed partial class HeroDesign
{
    // (the blade, in the sword hand's space: it runs down -Y from the grip, broad along X, its point here)
    private const float BladeX = 0.016f, BladeTip = -1.12f;

    private static Shader _frostGrow;
    /// <summary>Ice that grows out of frost: the gradient along UV.x (out from the blade; <paramref name="along"/>: UV.y, up a crystal).</summary>
    private static ShaderMaterial AxeIce(bool along)
    {
        _frostGrow ??= GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_frostgrow.gdshader");
        var m = new ShaderMaterial { Shader = _frostGrow };
        m.SetShaderParameter("axis", along ? 1 : 0);
        return m;
    }

    /// <summary>The two axe heads, hidden until a swing grows one.</summary>
    private static void BuildIceAxes(Node3D hand)
    {
        ShaderMaterial outward = AxeIce(false), up = AxeIce(true);
        // ---- the Ice Axe: a crescent bit on each side of the blade near its point, a spike out of the point
        var axe = new Node3D { Name = "IceAxe", Visible = false, Position = new Vector3(BladeX, BladeTip + 0.22f, 0f) };
        hand.AddChild(axe);
        var bit = AxeBit(0.4f, 0.5f, 0.2f, 0.1f, 0.06f, false).ToMesh();
        foreach (int side in new[] { -1, 1 })
            axe.AddChild(new MeshInstance3D { Name = side < 0 ? "BitL" : "BitR", Mesh = bit, MaterialOverride = outward, RotationDegrees = new Vector3(0, side < 0 ? 180f : 0f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var spike = new MeshBuilder();
        spike.Crystal(0.05f, 0.3f, 0.14f, new Color(0.92f, 0.99f, 1f));
        axe.AddChild(new MeshInstance3D { Name = "Spike", Mesh = spike.ToMesh(), MaterialOverride = up, Position = new Vector3(0, -0.22f, 0), RotationDegrees = new Vector3(180f, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        axe.AddChild(new OmniLight3D { Name = "Light", LightColor = new Color(0.55f, 0.85f, 1f), LightEnergy = 0f, OmniRange = 3.5f, ShadowEnabled = false });

        // ---- the Glacier Cleaver: a haft of ice running on from the point, and at its end a vast, horned, crowned head
        var glacier = new Node3D { Name = "GlacierAxe", Visible = false, Position = new Vector3(BladeX, BladeTip + 0.05f, 0f) };
        hand.AddChild(glacier);
        const float haftLen = 1.5f;
        var haft = new MeshBuilder();
        haft.Crystal(0.075f, 1f, 0.06f, new Color(0.75f, 0.92f, 1f));
        // (unit length, stretched as it grows: it runs down -Y)
        glacier.AddChild(new MeshInstance3D { Name = "Haft", Mesh = haft.ToMesh(), MaterialOverride = up, RotationDegrees = new Vector3(180f, 0, 0), Scale = new Vector3(1f, haftLen, 1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var head = new Node3D { Name = "Head", Position = new Vector3(0, -haftLen + 0.35f, 0) };
        glacier.AddChild(head);
        var big = AxeBit(1.05f, 1.35f, 0.42f, 0.26f, 0.13f, true).ToMesh();
        foreach (int side in new[] { -1, 1 })
            head.AddChild(new MeshInstance3D { Name = side < 0 ? "BitL" : "BitR", Mesh = big, MaterialOverride = outward, RotationDegrees = new Vector3(0, side < 0 ? 180f : 0f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // the ornaments: a collar of crystals where the bits meet the haft, studs along the bits, and a crown spike at the end
        var orn = new MeshBuilder();
        var rng = new Random(7);
        for (int k = 0; k < 6; k++)
        {
            var c = new MeshBuilder();
            c.Crystal(0.05f, 0.24f + 0.1f * (float)rng.NextDouble(), 0.1f, new Color(0.9f, 0.98f, 1f), k);
            float a = k * Mathf.Tau / 6f;
            var outDir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a) * 0.5f).Normalized();
            orn.Append(c, new Transform3D(new Basis(outDir.Cross(Vector3.Up).Normalized(), 1.1f), new Vector3(0, 0.45f + (k % 2) * 0.12f, 0)));
        }
        foreach (int side in new[] { -1, 1 })
            for (int k = 0; k < 3; k++)
            {
                var c = new MeshBuilder();
                c.Crystal(0.045f, 0.2f, 0.09f, new Color(0.85f, 0.97f, 1f), k);
                float x = side * (0.3f + 0.25f * k), y = (k - 1) * 0.28f;
                orn.Append(c, new Transform3D(new Basis(Vector3.Right, side * 1.57f) * new Basis(Vector3.Forward, 0.3f * side), new Vector3(x, y, 0.06f)));
                orn.Append(c, new Transform3D(new Basis(Vector3.Right, -side * 1.57f) * new Basis(Vector3.Forward, 0.3f * side), new Vector3(x, y, -0.06f)));
            }
        head.AddChild(new MeshInstance3D { Name = "Ornament", Mesh = orn.ToMesh(), MaterialOverride = up, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var crown = new MeshBuilder();
        crown.Crystal(0.11f, 0.7f, 0.3f, new Color(0.95f, 1f, 1f));
        for (int s = -1; s <= 1; s += 2)
        {
            var c = new MeshBuilder();
            c.Crystal(0.06f, 0.4f, 0.18f, new Color(0.9f, 0.98f, 1f));
            crown.Append(c, new Transform3D(new Basis(Vector3.Back, s * 0.45f), new Vector3(s * 0.06f, 0.05f, 0)));
        }
        head.AddChild(new MeshInstance3D { Name = "Crown", Mesh = crown.ToMesh(), MaterialOverride = up, Position = new Vector3(0, -0.35f, 0), RotationDegrees = new Vector3(180f, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        glacier.AddChild(new OmniLight3D { Name = "Light", LightColor = new Color(0.55f, 0.85f, 1f), LightEnergy = 0f, OmniRange = 6f, ShadowEnabled = false, Position = new Vector3(0, -haftLen, 0.3f) });
    }

    /// <summary>
    /// One bit of an axe head in ice, growing out along +X from the blade at x = 0: a narrow neck (<paramref name="neck"/> high) flaring to a
    /// broad crescent edge (<paramref name="edge"/> high, bulging <paramref name="bulge"/> past <paramref name="width"/>), thick at the neck,
    /// thin at the edge. Ornate: horns curling out from the edge's ends.
    /// </summary>
    private static MeshBuilder AxeBit(float width, float edge, float neck, float bulge, float thick, bool ornate)
    {
        float w = width, e = edge * 0.5f, n = neck * 0.5f, f = bulge;
        var top = new List<Vector2> { new(0, n), new(0.35f * w, n * 1.1f), new(0.7f * w, e * 0.72f) };
        if (ornate) { top.Add(new(0.88f * w, e * 0.86f)); top.Add(new(w + f * 0.75f, e * 1.32f)); top.Add(new(0.97f * w, e)); }
        else top.Add(new(w, e));
        top.Add(new(w + f * 0.6f, e * 0.5f));
        // the outline, counter-clockwise from the neck's foot: along the bottom out to the edge, round it, back along the top
        var pts = new List<Vector2>();
        foreach (var p in top) pts.Add(new Vector2(p.X, -p.Y));
        pts.Add(new Vector2(w + f, 0));
        for (int k = top.Count - 1; k >= 0; k--) pts.Add(top[k]);
        var mb = new MeshBuilder();
        var inner = new Color(0.62f, 0.86f, 1f);
        var outer = new Color(0.96f, 1f, 1f);
        float Z(Vector2 p) => thick * 0.5f * (1f - 0.75f * Math.Clamp(p.X / (w + f), 0f, 1f));
        Color Col(Vector2 p) => inner.Lerp(outer, Math.Clamp(p.X / (w + f), 0f, 1f));
        // (the gradient the frost grows along: out from the blade)
        Vector2 Uv(Vector2 p) => new(Math.Clamp(p.X / (w + f), 0f, 1f), Math.Clamp(p.Y / (e * 1.4f) * 0.5f + 0.5f, 0f, 1f));
        var centre = new Vector2(0.6f * w, 0);
        foreach (int face in new[] { 1, -1 })
        {
            var nrm = new Vector3(0, 0, face);
            int c = mb.Add(new Vector3(centre.X, 0, face * Z(centre)), nrm, Col(centre), Uv(centre));
            for (int k = 0; k < pts.Count - 1; k++)
            {
                int a = mb.Add(new Vector3(pts[k].X, pts[k].Y, face * Z(pts[k])), nrm, Col(pts[k]), Uv(pts[k]));
                int b = mb.Add(new Vector3(pts[k + 1].X, pts[k + 1].Y, face * Z(pts[k + 1])), nrm, Col(pts[k + 1]), Uv(pts[k + 1]));
                if (face > 0) mb.Tri(c, a, b); else mb.Tri(c, b, a);
            }
            // (the neck closes the fan where it meets the blade)
            int na = mb.Add(new Vector3(pts[^1].X, pts[^1].Y, face * Z(pts[^1])), nrm, Col(pts[^1]), Uv(pts[^1]));
            int nb = mb.Add(new Vector3(pts[0].X, pts[0].Y, face * Z(pts[0])), nrm, Col(pts[0]), Uv(pts[0]));
            if (face > 0) mb.Tri(c, na, nb); else mb.Tri(c, nb, na);
        }
        // the rim: a band round the outline joining the faces
        for (int k = 0; k < pts.Count - 1; k++)
        {
            var p = pts[k]; var q = pts[k + 1];
            var d = q - p;
            var nrm = new Vector3(d.Y, -d.X, 0).Normalized();
            int a = mb.Add(new Vector3(p.X, p.Y, Z(p)), nrm, Col(p), Uv(p)), b = mb.Add(new Vector3(q.X, q.Y, Z(q)), nrm, Col(q), Uv(q));
            int c = mb.Add(new Vector3(q.X, q.Y, -Z(q)), nrm, Col(q), Uv(q)), dd = mb.Add(new Vector3(p.X, p.Y, -Z(p)), nrm, Col(p), Uv(p));
            mb.Quad(a, dd, c, b);
        }
        return mb;
    }

    /// <summary>Grows and lights the axe heads by the swing: each part draws itself out of frost in its turn (the shader's growth front).</summary>
    private static void SyncIceAxes(CreatureModel m, Player player, float time)
    {
        var axe = m.GetNodeOrNull<Node3D>("Pivot/Skeleton/BladeHand/IceAxe");
        var glacier = m.GetNodeOrNull<Node3D>("Pivot/Skeleton/BladeHand/GlacierAxe");
        if (axe == null || glacier == null) return;
        int kind = player != null && !player.Dead ? player.AxeKind : 0;
        float grow = kind == 0 ? 0f : player.AxeGrow;
        float stage = kind == 0 ? 0f : player.AxeStage;
        // (a white flare as it comes down)
        float flare = stage >= 1f && stage < 1.6f ? 1f - (stage - 1f) / 0.6f : 0f;
        axe.Visible = kind == 1 && grow > 0.01f;
        glacier.Visible = kind == 2 && grow > 0.01f;
        if (axe.Visible)
        {
            Grow(axe, "BitL", W3.SmoothStep(0f, 0.85f, grow), flare);
            Grow(axe, "BitR", W3.SmoothStep(0.05f, 0.9f, grow), flare);
            Grow(axe, "Spike", W3.SmoothStep(0.5f, 1f, grow), flare);
            if (axe.GetNodeOrNull<OmniLight3D>("Light") is { } l) l.LightEnergy = 1.6f * grow + 2.5f * flare;
        }
        if (glacier.Visible)
        {
            // the haft runs on first, then the bits flare out of its end, then the crown and the crystals
            Grow(glacier, "Haft", W3.SmoothStep(0f, 0.42f, grow), flare);
            Grow(glacier, "Head/BitL", W3.SmoothStep(0.3f, 0.86f, grow), flare);
            Grow(glacier, "Head/BitR", W3.SmoothStep(0.34f, 0.9f, grow), flare);
            Grow(glacier, "Head/Ornament", W3.SmoothStep(0.68f, 1f, grow), flare);
            Grow(glacier, "Head/Crown", W3.SmoothStep(0.6f, 1f, grow), flare);
            if (glacier.GetNodeOrNull<OmniLight3D>("Light") is { } l) l.LightEnergy = 2.4f * grow + 3.5f * flare;
        }
    }

    private static void Grow(Node3D root, string path, float grow, float flare)
    {
        if (root.GetNodeOrNull<MeshInstance3D>(path) is not { } mi) return;
        mi.Visible = grow > 0.005f;
        mi.SetInstanceShaderParameter("grow", grow);
        mi.SetInstanceShaderParameter("flare", flare);
    }

    /// <summary>
    /// The axe's swing (<see cref="Player.AxeStage"/>: 0-1 the wind-up, 1-2 the blow, 2-3 after): both hands on the hilt, the sword hauled up
    /// over the head and far behind, and held there, arched back and sinking under its weight as the head grows, trembling with it; then
    /// brought over and down in front in one fast arc into a deep lunge. The Glacier Cleaver's head is heavier: he arches further.
    /// </summary>
    private static Body AxeHeave(Body basePose, float stage, bool grounded, int kind, float time)
    {
        const float raise = 0.22f;
        float heavy = kind == 2 ? 1.6f : 1f;
        float blade, lean, twist, bend, head, low;
        if (stage < raise)
        {
            float k = W3.Smooth01(stage / raise);
            blade = Mathf.Lerp(basePose.SR - 90f + basePose.ER * 0.5f, 122f, k);
            lean = Mathf.Lerp(basePose.Lean, -18f, k);
            twist = -30f * k;
            bend = Mathf.Lerp(40f, 84f, k);
            head = -18f * k;
            low = 0.3f * k;
        }
        else if (stage < 1f)
        {
            float h = (stage - raise) / (1f - raise);
            // (held up behind, sinking back as it grows heavier, trembling; a last haul back just before it comes)
            float tremble = MathF.Sin(time * 31f) * (1.5f + 2.5f * h) * heavy;
            float haul = W3.SmoothStep(0.82f, 1f, h);
            blade = 122f + 8f * h * heavy + 8f * haul + tremble;
            lean = -18f - 9f * h * heavy - 5f * haul;
            twist = -30f - 6f * haul;
            bend = 84f + 6f * haul;
            head = -18f - 8f * h;
            low = 0.3f + 0.35f * h * heavy;
        }
        else if (stage < 2f)
        {
            float k = stage - 1f;
            k = 1f - (1f - k) * (1f - k);
            float from = 138f + 8f * (heavy - 1f);
            blade = Mathf.Lerp(from, -55f, k);
            lean = Mathf.Lerp(-32f - 9f * (heavy - 1f), 46f, k);
            twist = Mathf.Lerp(-36f, 32f, k);
            bend = Mathf.Lerp(90f, 6f, k);
            head = Mathf.Lerp(-26f, 10f, k);
            low = Mathf.Lerp(0.65f, 1f, k);
        }
        else
        {
            float k = W3.SmoothStep(0.2f, 1f, stage - 2f);
            blade = Mathf.Lerp(-55f, -40f, k);
            lean = Mathf.Lerp(46f, 18f, k);
            twist = Mathf.Lerp(32f, 10f, k);
            bend = 8f;
            head = 10f;
            low = Mathf.Lerp(1f, 0.6f, k);
        }
        var o = basePose;
        o.SR = 90f + blade - bend * 0.35f; o.ER = bend; o.WR = -bend * 0.55f; o.AR = 14f; o.WRy = 0;
        // the off hand joins the sword hand on the hilt
        o.SL = o.SR - 10f; o.EL = bend + 12f; o.AL = 26f; o.WL = o.WR;
        o.Lean = lean; o.Twist = twist; o.HipTwist = -twist * 0.45f;
        o.HeadPitch = head;
        if (grounded)
        {
            low = Math.Clamp(low, 0f, 1f);
            o.HR = Mathf.Lerp(o.HR, 50f, low); o.KR = Mathf.Lerp(o.KR, 68f, low);
            o.HL = Mathf.Lerp(o.HL, -30f, low); o.KL = Mathf.Lerp(o.KL, 24f, low);
            o.FR = Mathf.Lerp(o.FR, 0f, low); o.FL = Mathf.Lerp(o.FL, -20f, low);
            o.Root.Y = Mathf.Lerp(o.Root.Y, -0.15f, low);
        }
        return o;
    }
}
