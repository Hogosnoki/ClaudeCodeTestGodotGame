using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Renders the game's effects in 3D. The FxLayer still simulates every particle (in the 2D
/// world's pixels, with a depth of its own); this draws them each frame as instanced quads and
/// meshes: additive HDR light for sparks, flashes, embers, rings and swooshes (they bloom), lit
/// soft smoke and dust, real tumbling rock for debris, glassy bubbles, and brief real lights at
/// every flash. It also draws the hero's blade smear and the Warden's guard.
/// </summary>
public partial class Fx3D : Node3D
{
    private const int Stride = 20; // 12 transform + 4 colour + 4 custom floats per instance

    private sealed class Batch
    {
        public MultiMeshInstance3D Node;
        public MultiMesh Mm;
        public float[] Buf;
        public int Count;

        public void Begin() => Count = 0;

        public void Add(in Transform3D t, Color c, Vector4 custom)
        {
            if (Count >= Mm.InstanceCount)
            {
                // grow (the multimesh buffer must match the instance count exactly)
                int cap = Mm.InstanceCount * 2;
                Mm.InstanceCount = cap;
                Array.Resize(ref Buf, cap * Stride);
            }
            int o = Count * Stride;
            var b = t.Basis;
            Buf[o] = b.Column0.X; Buf[o + 1] = b.Column1.X; Buf[o + 2] = b.Column2.X; Buf[o + 3] = t.Origin.X;
            Buf[o + 4] = b.Column0.Y; Buf[o + 5] = b.Column1.Y; Buf[o + 6] = b.Column2.Y; Buf[o + 7] = t.Origin.Y;
            Buf[o + 8] = b.Column0.Z; Buf[o + 9] = b.Column1.Z; Buf[o + 10] = b.Column2.Z; Buf[o + 11] = t.Origin.Z;
            Buf[o + 12] = c.R; Buf[o + 13] = c.G; Buf[o + 14] = c.B; Buf[o + 15] = c.A;
            Buf[o + 16] = custom.X; Buf[o + 17] = custom.Y; Buf[o + 18] = custom.Z; Buf[o + 19] = custom.W;
            Count++;
        }

        public void End()
        {
            if (Count > 0) RenderingServer.MultimeshSetBuffer(Mm.GetRid(), Buf);
            Mm.VisibleInstanceCount = Count;
        }
    }

    private Batch _glow, _smoke, _debris, _bubbles;
    private readonly OmniLight3D[] _lights = new OmniLight3D[10];
    private readonly float[] _lt = new float[10], _lmax = new float[10], _le = new float[10];
    private MeshInstance3D _ribbon, _barrier;
    private ImmediateMesh _rmesh;
    private OmniLight3D _bladeLight;
    private ShaderMaterial _barrierMat;
    private float _time;

    private static Batch MakeBatch(Node parent, string name, Mesh mesh, Material mat, int cap = 256)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = mesh,
            InstanceCount = cap,
            VisibleInstanceCount = 0,
        };
        var node = new MultiMeshInstance3D
        {
            Name = name, Multimesh = mm, MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // effects are everywhere; never cull them by the (tiny) mesh bounds
            CustomAabb = new Aabb(new Vector3(-5000, -5000, -50), new Vector3(10000, 10000, 100)),
        };
        parent.AddChild(node);
        return new Batch { Node = node, Mm = mm, Buf = new float[cap * Stride] };
    }

    public override void _Ready()
    {
        Name = "Fx3D";
        var quad = new QuadMesh { Size = new Vector2(2, 2) };
        _glow = MakeBatch(this, "Glow", quad, new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_glow.gdshader") }, 512);
        _smoke = MakeBatch(this, "Smoke", quad, new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_smoke.gdshader") }, 256);
        var rock = new MeshBuilder();
        rock.Blob(Vector3.Zero, new Vector3(1f, 0.8f, 0.9f), 4, new Color(1, 1, 1), new Noise3(11), 0.3f, 1.6f);
        var rockMesh = rock.ToMesh();
        _debris = MakeBatch(this, "Debris", rockMesh, new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f }, 128);
        _debris.Node.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 };
        _bubbles = MakeBatch(this, "Bubbles", sphere, new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_bubble.gdshader") }, 64);

        for (int k = 0; k < _lights.Length; k++)
        {
            _lights[k] = new OmniLight3D { Visible = false, ShadowEnabled = false, LightVolumetricFogEnergy = 1.5f, OmniAttenuation = 1.4f, LightSpecular = 0.6f };
            AddChild(_lights[k]);
        }

        _rmesh = new ImmediateMesh();
        _ribbon = new MeshInstance3D
        {
            Name = "Ribbons", Mesh = _rmesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_ribbon.gdshader") },
            CustomAabb = new Aabb(new Vector3(-5000, -5000, -50), new Vector3(10000, 10000, 100)),
        };
        AddChild(_ribbon);
        _bladeLight = new OmniLight3D { Visible = false, ShadowEnabled = false, OmniRange = 3.5f, LightEnergy = 0f, LightVolumetricFogEnergy = 0.8f };
        AddChild(_bladeLight);

        _barrierMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://DaggerCave/Render3D/Shaders/fx_ghost.gdshader") };
        _barrier = new MeshInstance3D
        {
            Name = "Barrier", Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 }, MaterialOverride = _barrierMat,
            Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_barrier);
    }

    private static Color Lin(Color c) => new Color(c.R, c.G, c.B).SrgbToLinear() with { A = c.A };

    private static Transform3D Quad(Vector3 at, float angle, float sx, float sy)
    {
        var b = new Basis(Vector3.Back, angle) * Basis.FromScale(new Vector3(Math.Max(1e-4f, sx), Math.Max(1e-4f, sy), 1f));
        return new Transform3D(b, at);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        var fx = G.Fx;
        _glow.Begin(); _smoke.Begin(); _debris.Begin(); _bubbles.Begin();
        if (fx != null && IsInstanceValid(fx))
        {
            DrawParticles(fx);
            TakeFlashes(fx);
        }
        DrawStunStars();
        _glow.End(); _smoke.End(); _debris.End(); _bubbles.End();
        UpdateLights(dt);
        DrawHero();
    }

    private void DrawParticles(FxLayer fx)
    {
        const float P = 1f / W3.Ppu;
        foreach (var p in fx.Particles)
        {
            float a = Math.Clamp(p.Life / p.Max, 0f, 1f);
            var at = W3.P(p.Pos, p.Z);
            var col = Lin(p.Col);
            float velAng = MathF.Atan2(-p.Vel.Y, p.Vel.X);
            float speed = p.Vel.Length();
            switch (p.Kind)
            {
                case 0: // dot (also trails)
                    {
                        float r = p.Size * (0.4f + 0.6f * a) * P * 2.2f;
                        _glow.Add(Quad(at, 0, r, r), col with { A = col.A * a }, new Vector4(0, 2f, 0, p.Seed));
                        break;
                    }
                case 1: // streak
                    {
                        float len = Math.Max(p.Size, speed * 0.03f) * P;
                        var dir = new Vector3(MathF.Cos(velAng), MathF.Sin(velAng), 0);
                        _glow.Add(Quad(at - dir * len * 0.5f, velAng, len * 0.6f + p.Size * P, p.Size * P * 1.4f), col with { A = col.A * a }, new Vector4(1, 2.2f, 0, p.Seed));
                        break;
                    }
                case 2: // bubble
                    {
                        float r = p.Size * P;
                        _bubbles.Add(new Transform3D(Basis.FromScale(Vector3.One * r), at), new Color(0.75f, 0.95f, 1f, a), Vector4.Zero);
                        break;
                    }
                case 3: // ring of light
                    {
                        float r = p.Size * (1.5f - a * 0.5f) * P * 1.25f;
                        _glow.Add(Quad(at, 0, r, r), col with { A = col.A * a }, new Vector4(2, 2.5f, 0.06f + 0.05f * a, p.Seed));
                        break;
                    }
                case 5: // spark: a hot white streak with a coloured sheath
                    {
                        float len = Math.Max(0.05f, speed * 0.05f * P);
                        var dir = new Vector3(MathF.Cos(velAng), MathF.Sin(velAng), 0);
                        float w = (p.Size * a + 0.6f) * P * 1.6f;
                        _glow.Add(Quad(at - dir * len * 0.5f, velAng, len * 0.55f, w), col with { A = a }, new Vector4(1, 6f, 0, p.Seed));
                        break;
                    }
                case 6: // impact flash
                    {
                        float r = p.Size * (1.2f - a * 0.4f) * P * 1.4f;
                        _glow.Add(Quad(at, 0, r, r), col with { A = a }, new Vector4(4, 3f, 0, p.Seed));
                        break;
                    }
                case 7: // flash
                    {
                        float r = p.Size * (1.25f - 0.5f * a) * P * 1.3f;
                        _glow.Add(Quad(at, 0, r, r), col with { A = a }, new Vector4(4, 1.8f, 0, p.Seed));
                        break;
                    }
                case 8: // shockwave: a ring racing out along the ground, and a faint wall of air
                    {
                        float r = p.Size * (1.1f - a) * P * 1.3f;
                        var ground = new Basis(Vector3.Right, -MathF.PI / 2f) * Basis.FromScale(new Vector3(r, r, 1));
                        _glow.Add(new Transform3D(ground, W3.P(p.Pos, 0f)), col with { A = col.A * a }, new Vector4(2, 3.5f, 0.05f + 0.04f * a, p.Seed));
                        _glow.Add(Quad(W3.P(p.Pos, 0.3f), 0, r, r * 0.35f), col with { A = col.A * a * 0.35f }, new Vector4(2, 2f, 0.1f, p.Seed));
                        break;
                    }
                case 9: // smoke and dust
                    {
                        float r = p.Size * (1.6f - 0.6f * a) * P * 1.6f;
                        _smoke.Add(Quad(at, p.Rot, r, r), new Color(col.R, col.G, col.B, p.Col.A * a * 0.85f), new Vector4(0, 0, 0, p.Seed));
                        break;
                    }
                case 10: // debris: tumbling chunks
                    {
                        float s = p.Size * P * 1.1f;
                        var b = new Basis(Vector3.Back, p.Rot) * new Basis(Vector3.Right, p.Rot * 0.7f + p.Seed * 6f) * Basis.FromScale(new Vector3(s, s * 0.8f, s * 0.9f));
                        _debris.Add(new Transform3D(b, at), new Color(p.Col.R, p.Col.G, p.Col.B, 1f), Vector4.Zero);
                        break;
                    }
                case 11: // droplets
                    {
                        float len = Math.Max(p.Size * 1.5f, speed * 0.025f) * P;
                        var dir = new Vector3(MathF.Cos(velAng), MathF.Sin(velAng), 0);
                        _smoke.Add(Quad(at - dir * len * 0.4f, velAng, len * 0.6f, p.Size * P * 0.9f), new Color(col.R, col.G, col.B, Math.Min(1f, p.Col.A * a * 1.2f)), new Vector4(1, 0, 0, p.Seed));
                        break;
                    }
                case 12: // glint
                    {
                        float s = p.Size * MathF.Sin(a * MathF.PI) * P * 1.6f;
                        _glow.Add(Quad(at, p.Rot, s, s), col with { A = a }, new Vector4(3, 4f, 0, p.Seed));
                        break;
                    }
                case 13: // ember: flickering
                    {
                        float fl = 0.6f + 0.4f * MathF.Sin(p.Life * 40 + p.Rot * 5);
                        float r = p.Size * P * 3f;
                        _glow.Add(Quad(at, 0, r, r), col with { A = a * fl }, new Vector4(0, 4f, 0, p.Seed));
                        break;
                    }
                case 14: // swoosh: a crescent sweeping from above to below on the facing side
                    {
                        float dir = p.Vel.X >= 0 ? 1 : -1;
                        float sweep = 2.6f * (1.15f - a * 0.4f);
                        // 2D angles are y-down; flip to y-up and mirror for facing left
                        float a0 = 1.3f, a1 = 1.3f - sweep;
                        float r = p.Size * P * 1.25f;
                        var b = Basis.FromScale(new Vector3(r * dir, r, 1));
                        _glow.Add(new Transform3D(b, at), col with { A = col.A * a }, new Vector4(5, 3f, a0, a1));
                        break;
                    }
            }
        }
    }

    private void DrawStunStars()
    {
        foreach (var e in G.Enemies)
        {
            if (e is not Bear bear || !IsInstanceValid(bear) || !bear.Stunned) continue;
            var head = W3.P(bear.GlobalPosition + new Vector2(0, -bear.HitRadius - 6));
            for (int k = 0; k < 3; k++)
            {
                float a = _time * 5f + k * Mathf.Tau / 3f;
                var at = head + new Vector3(MathF.Cos(a) * 0.75f, MathF.Sin(a * 2f) * 0.05f, MathF.Sin(a) * 0.75f);
                _glow.Add(Quad(at, _time * 3f, 0.22f, 0.22f), Lin(new Color(1f, 0.95f, 0.5f)), new Vector4(3, 4f, 0, k));
                _glow.Add(Quad(at, 0, 0.15f, 0.15f), Lin(new Color(1f, 0.9f, 0.4f, 0.8f)), new Vector4(0, 2f, 0, k));
            }
        }
    }

    // ---- brief real lights at flashes, explosions and big hits
    private void TakeFlashes(FxLayer fx)
    {
        foreach (var f in fx.Flashes)
        {
            // take a free light, or the one closest to going out
            int best = 0;
            float bestE = float.MaxValue;
            for (int k = 0; k < _lights.Length; k++)
            {
                float e = _lt[k] <= 0 ? -1f : _le[k] * (_lt[k] / Math.Max(1e-3f, _lmax[k]));
                if (e < bestE) { bestE = e; best = k; }
            }
            var l = _lights[best];
            l.Position = W3.P(f.Pos, 1.2f);
            l.LightColor = new Color(f.Col.R, f.Col.G, f.Col.B).Lerp(Colors.White, 0.25f);
            l.OmniRange = Math.Clamp(f.Range / W3.Ppu, 1.5f, 16f);
            _le[best] = f.Energy;
            _lt[best] = _lmax[best] = Math.Max(0.05f, f.Life);
            l.Visible = true;
        }
        fx.Flashes.Clear();
    }

    private void UpdateLights(float dt)
    {
        for (int k = 0; k < _lights.Length; k++)
        {
            if (_lt[k] <= 0) { if (_lights[k].Visible) _lights[k].Visible = false; continue; }
            _lt[k] -= dt;
            float f = Math.Clamp(_lt[k] / _lmax[k], 0f, 1f);
            _lights[k].LightEnergy = _le[k] * f * f;
        }
    }

    // ---- the hero's blade smear and the Warden's guard
    private void DrawHero()
    {
        _rmesh.ClearSurfaces();
        bool any = false, blade = false;
        _barrier.Visible = false;
        // every hero in the game (online, the others' too)
        foreach (var p in G.Players)
        {
            if (p == null || !IsInstanceValid(p)) continue;
            if (p.GetSmear(out var sm)) { SmearRibbon(sm); any = true; blade = true; }
            var g = p.GetGuard();
            if (g.Shield) { GuardArc(p, g); any = true; }
            if (g.Dash)
            {
                // the shield dash: a shell of blue light around the charging Warden
                _barrier.Visible = true;
                _barrier.GlobalTransform = new Transform3D(Basis.FromScale(Vector3.One * 17f / W3.Ppu), W3.P(p.GlobalPosition + new Vector2(0, -2), 0f));
                _barrier.SetInstanceShaderParameter("ghost_color", new Color(0.45f, 0.75f, 1f, 0.55f));
            }
        }
        if (!blade) _bladeLight.Visible = false;
        _ribbon.Visible = any;
    }

    private void SmearRibbon(Player.Smear sm)
    {
        const int n = 20;
        const float z = 0.45f;
        var tint = Lin(sm.Tint);
        _rmesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Vector3 Pt(float ang, float r) => W3.P(sm.Origin + Vector2.Right.Rotated(ang) * r, z);
        for (int k = 0; k < n - 1; k++)
        {
            for (int band = 0; band < 2; band++)
            {
                // two bands: the bright outer edge and a softer inner sheet toward the hand
                float t0 = k / (float)(n - 1), t1 = (k + 1) / (float)(n - 1);
                float w0 = sm.Blade * (0.35f + 0.65f * MathF.Sin(t0 * MathF.PI * 0.5f)), w1 = sm.Blade * (0.35f + 0.65f * MathF.Sin(t1 * MathF.PI * 0.5f));
                float an0 = Mathf.Lerp(sm.Tail, sm.Head, t0), an1 = Mathf.Lerp(sm.Tail, sm.Head, t1);
                float al0 = t0 * t0 * sm.Fade, al1 = t1 * t1 * sm.Fade;
                float ro0 = band == 0 ? sm.Outer : sm.Outer - w0 * 0.4f, ro1 = band == 0 ? sm.Outer : sm.Outer - w1 * 0.4f;
                float ri0 = band == 0 ? sm.Outer - w0 * 0.4f : sm.Outer - w0, ri1 = band == 0 ? sm.Outer - w1 * 0.4f : sm.Outer - w1;
                float ao0 = band == 0 ? 0.9f : 0.2f, ai0 = band == 0 ? 0.2f : 0f;
                Color c(float alpha) => new(tint.R, tint.G, tint.B, alpha);
                var A = Pt(an0, ro0); var B = Pt(an1, ro1); var C = Pt(an1, ri1); var D = Pt(an0, ri0);
                _rmesh.SurfaceSetColor(c(ao0 * al0)); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(c(ao0 * al1)); _rmesh.SurfaceAddVertex(B);
                _rmesh.SurfaceSetColor(c(ai0 * al1)); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(c(ao0 * al0)); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(c(ai0 * al1)); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(c(ai0 * al0)); _rmesh.SurfaceAddVertex(D);
            }
            // a white-hot edge line along the outer rim
            {
                float t0 = k / (float)(n - 1), t1 = (k + 1) / (float)(n - 1);
                float an0 = Mathf.Lerp(sm.Tail, sm.Head, t0), an1 = Mathf.Lerp(sm.Tail, sm.Head, t1);
                float al0 = t0 * t0 * sm.Fade, al1 = t1 * t1 * sm.Fade;
                float th = sm.Finisher || sm.Charged ? 1.4f : 1f;
                var A = Pt(an0, sm.Outer + th); var B = Pt(an1, sm.Outer + th); var C = Pt(an1, sm.Outer - th); var D = Pt(an0, sm.Outer - th);
                var w0 = new Color(1, 1, 1, al0 * 1.4f); var w1 = new Color(1, 1, 1, al1 * 1.4f);
                _rmesh.SurfaceSetColor(w0); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(w1); _rmesh.SurfaceAddVertex(B);
                _rmesh.SurfaceSetColor(w1); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(w0); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(w1); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(w0); _rmesh.SurfaceAddVertex(D);
            }
        }
        _rmesh.SurfaceEnd();
        // the blade's edge lights what it passes
        _bladeLight.Visible = true;
        _bladeLight.Position = Pt(sm.Head, sm.Outer * 0.8f);
        _bladeLight.LightColor = sm.Tint;
        _bladeLight.LightEnergy = (sm.Finisher || sm.Charged ? 2.2f : 1.3f) * sm.Fade;
    }

    private void GuardArc(Player p, Player.Guard g)
    {
        const int n = 16;
        var o = p.GlobalPosition + new Vector2(0, -3);
        var col = Lin(g.Col);
        _rmesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int k = 0; k < n - 1; k++)
        {
            float a0 = g.Angle - g.Half + 2f * g.Half * k / (n - 1), a1 = g.Angle - g.Half + 2f * g.Half * (k + 1) / (n - 1);
            float edge0 = MathF.Sin(k / (float)(n - 1) * MathF.PI), edge1 = MathF.Sin((k + 1) / (float)(n - 1) * MathF.PI);
            float grow = g.Dash ? 1.35f : 1f; // the charge leads with a bigger, brighter guard
            foreach (var (r0, r1, alpha) in new[] { (14f * grow, 20f * grow, (0.35f + 0.35f * g.Strength) * grow), (18f * grow, 19.6f * grow, (0.8f + 0.6f * g.Strength) * grow) })
            {
                var A = W3.P(o + Vector2.Right.Rotated(a0) * r1, 0.4f); var B = W3.P(o + Vector2.Right.Rotated(a1) * r1, 0.4f);
                var C = W3.P(o + Vector2.Right.Rotated(a1) * r0, 0.4f); var D = W3.P(o + Vector2.Right.Rotated(a0) * r0, 0.4f);
                var c0 = new Color(col.R, col.G, col.B, alpha * (0.3f + 0.7f * edge0));
                var c1 = new Color(col.R, col.G, col.B, alpha * (0.3f + 0.7f * edge1));
                _rmesh.SurfaceSetColor(c0); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(c1); _rmesh.SurfaceAddVertex(B);
                _rmesh.SurfaceSetColor(c1 with { A = c1.A * 0.2f }); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(c0); _rmesh.SurfaceAddVertex(A);
                _rmesh.SurfaceSetColor(c1 with { A = c1.A * 0.2f }); _rmesh.SurfaceAddVertex(C);
                _rmesh.SurfaceSetColor(c0 with { A = c0.A * 0.2f }); _rmesh.SurfaceAddVertex(D);
            }
        }
        _rmesh.SurfaceEnd();
    }
}
