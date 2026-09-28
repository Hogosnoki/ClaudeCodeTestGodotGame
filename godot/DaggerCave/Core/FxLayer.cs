using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Lightweight particle / floating text / screen-shake system. It simulates in the 2D world's
/// pixels; the 3D stage (Fx3D, FxOverlay) renders what it holds, giving every particle a depth
/// of its own so bursts fill space instead of a plane.
/// </summary>
public partial class FxLayer : Node2D
{
    public struct Particle
    {
        public Vector2 Pos, Vel;
        public float Life, Max, Size, Grav, Drag;
        public Color Col;
        public int Kind; // 0 dot, 1 streak, 2 bubble, 3 ring, 5 spark streak, 6 impact flash, 7 flash, 8 shockwave,
                         // 9 smoke, 10 debris, 11 droplet, 12 glint, 13 ember, 14 swoosh
        public float Rot, Spin;
        /// <summary>Depth (metres toward the camera from the play plane) and its speed: 3D only.</summary>
        public float Z, ZVel;
        public float Seed;
    }

    public struct FloatText
    {
        public Vector2 Pos;
        public string Text;
        public Color Col;
        public float Life, Max;
        public int Size;
    }

    /// <summary>A brief light where something flashes (the 3D stage lights the scene with it).</summary>
    public struct LightFlash
    {
        public Vector2 Pos;
        public Color Col;
        public float Energy, Range, Life;
    }

    private readonly List<Particle> _parts = new(1024);
    private readonly List<FloatText> _texts = new();
    public float Shake;
    public IReadOnlyList<Particle> Particles => _parts;
    public IReadOnlyList<FloatText> Texts => _texts;
    /// <summary>Flashes since the renderer last took them.</summary>
    public readonly List<LightFlash> Flashes = new();
    // visual-only randomness (depth, seeds) never touches the gameplay RNG
    private readonly Random _vis = new(7);
    private float VR(float a, float b) => a + (float)_vis.NextDouble() * (b - a);

    /// <summary>Asks the 3D stage for a brief light here.</summary>
    public void LightUp(Vector2 pos, Color col, float energy, float rangePx, float life)
    {
        if (Flashes.Count > 24) Flashes.RemoveAt(0);
        Flashes.Add(new LightFlash { Pos = pos, Col = col, Energy = energy, Range = rangePx, Life = life });
    }

    /// <summary>Gives a particle its depth: things thrown outward also fly toward or away from the viewer.</summary>
    private Particle Deep(Particle p, float spread)
    {
        p.Z = 0.35f + VR(-0.15f, 0.15f);
        p.ZVel = spread > 0 ? VR(-1f, 1f) * spread : 0f;
        p.Seed = VR(0f, 1f);
        return p;
    }

    public override void _Ready() { ZIndex = 12; }

    // ---- online: effects made by things the other games need to see (the host's creatures,
    // this game's hero) are recorded and sent to them (see NetSync)
    public const byte SoundOp = 100;
    private int _quiet;
    private NetOut Rec(byte op) => _quiet == 0 && NetSync.Recording ? NetSync.FxBegin(op) : null;

    /// <summary>Plays an effect another game sent.</summary>
    public void ApplyNet(byte op, NetIn r)
    {
        switch (op)
        {
            case 1: Burst(r.Vec(), r.Col(), r.Byte(), r.Half(), r.Half(), r.Half(), r.Half(), r.Byte(), r.Half()); break;
            case 2: Directional(r.Vec(), r.HVec(), r.Half(), r.Col(), r.Byte(), r.Half(), r.Half(), r.Half(), r.Half(), r.Byte()); break;
            case 3: Bubbles(r.Vec(), r.Byte()); break;
            case 4: Spark(r.Vec(), r.HVec(), r.Bool(), r.Col()); break;
            case 5: Ring(r.Vec(), r.Half(), r.Col(), r.Half()); break;
            case 6: Text(r.Vec(), r.Str(), r.Col(), r.Byte(), r.Half()); break;
            case 7: Flash(r.Vec(), r.Half(), r.Col(), r.Half()); break;
            case 8: Shockwave(r.Vec(), r.Half(), r.Col(), r.Half()); break;
            case 9: { var at = r.Vec(); int n = r.Byte(); float sp = r.Half(); bool has = r.Bool(); Color? c = has ? r.Col() : null; Dust(at, n, sp, c); break; }
            case 10: Smoke(r.Vec(), r.Byte(), r.Col(), r.Half()); break;
            case 11: Debris(r.Vec(), r.Col(), r.Byte(), r.Half()); break;
            case 12: Splash(r.Vec(), r.Half(), r.Col()); break;
            case 13: Glint(r.Vec(), r.Col(), r.Half()); break;
            case 14: Ember(r.Vec(), r.Col()); break;
            case 15: Trail(r.Vec(), r.Col()); break;
            case 16: Mote(r.Vec(), r.Vec(), r.Col()); break;
            case 17: Converge(r.Vec(), r.Half(), r.Col(), r.Byte(), r.Half()); break;
            case 18: Beam(r.Vec(), r.Vec(), r.Col()); break;
            case 19: Swoosh(r.Vec(), r.Half(), r.Half(), r.Col()); break;
            case 20: Pop(r.Vec(), r.Col(), r.Half()); break;
            case 21: Explosion(r.Vec(), r.Col(), r.Half()); break;
            default: throw new InvalidOperationException($"unknown effect {op}");
        }
    }

    public void Burst(Vector2 pos, Color col, int n, float speed, float size = 2.5f, float life = 0.5f, float grav = 300f, int kind = 0, float drag = 1.5f)
    {
        Rec(1)?.Vec(pos).Col(col).Byte((byte)Math.Clamp(n, 0, 255)).Half(speed).Half(size).Half(life).Half(grav).Byte((byte)kind).Half(drag);
        for (int k = 0; k < n; k++)
        {
            var d = G.RandDir() * speed * G.Range(0.3f, 1f);
            _parts.Add(Deep(new Particle { Pos = pos, Vel = d, Life = life * G.Range(0.6f, 1.1f), Max = life, Size = size * G.Range(0.6f, 1.3f), Grav = grav, Drag = drag, Col = col, Kind = kind }, speed / 16f * 0.7f));
        }
    }

    public void Directional(Vector2 pos, Vector2 dir, float spread, Color col, int n, float speed, float size = 2f, float life = 0.4f, float grav = 200f, int kind = 1)
    {
        Rec(2)?.Vec(pos).HVec(dir).Half(spread).Col(col).Byte((byte)Math.Clamp(n, 0, 255)).Half(speed).Half(size).Half(life).Half(grav).Byte((byte)kind);
        for (int k = 0; k < n; k++)
        {
            var d = dir.Rotated(G.Range(-spread, spread)) * speed * G.Range(0.4f, 1f);
            _parts.Add(Deep(new Particle { Pos = pos, Vel = d, Life = life * G.Range(0.6f, 1.1f), Max = life, Size = size, Grav = grav, Drag = 2f, Col = col, Kind = kind }, speed / 16f * 0.35f));
        }
    }

    public void Bubbles(Vector2 pos, int n)
    {
        Rec(3)?.Vec(pos).Byte((byte)Math.Clamp(n, 0, 255));
        for (int k = 0; k < n; k++)
            _parts.Add(Deep(new Particle { Pos = pos + G.RandDir() * 4, Vel = new Vector2(G.Range(-15, 15), G.Range(-60, -20)), Life = G.Range(0.6f, 1.4f), Max = 1.4f, Size = G.Range(1.5f, 3.5f), Grav = -60, Drag = 1f, Col = new Color(0.75f, 0.95f, 1f, 0.8f), Kind = 2 }, 0.4f));
    }

    /// <summary>
    /// Hit impact: a flash disk plus a star of streaks thrown mostly along the blow's direction.
    /// </summary>
    public void Spark(Vector2 pos, Vector2 dir, bool big, Color col)
    {
        Rec(4)?.Vec(pos).HVec(dir).Bool(big).Col(col);
        if (dir.LengthSquared() < 0.01f) dir = Vector2.Right;
        dir = dir.Normalized();
        _parts.Add(Deep(new Particle { Pos = pos, Life = big ? 0.12f : 0.08f, Max = big ? 0.12f : 0.08f, Size = big ? 13 : 8, Col = col, Kind = 6 }, 0f));
        LightUp(pos, col, big ? 5f : 2.5f, big ? 90f : 55f, big ? 0.16f : 0.1f);
        int n = big ? 9 : 5;
        for (int k = 0; k < n; k++)
        {
            var d = (k < n / 2 + 1 ? dir.Rotated(G.Range(-0.6f, 0.6f)) : G.RandDir());
            float sp = G.Range(250, big ? 520 : 380);
            _parts.Add(Deep(new Particle { Pos = pos, Vel = d * sp, Life = G.Range(0.08f, 0.16f), Max = 0.16f, Size = big ? 2.2f : 1.6f, Grav = 0, Drag = 6f, Col = col, Kind = 5 }, sp / 16f * 0.5f));
        }
    }

    public void Ring(Vector2 pos, float radius, Color col, float life = 0.3f)
    {
        Rec(5)?.Vec(pos).Half(radius).Col(col).Half(life);
        _parts.Add(Deep(new Particle { Pos = pos, Life = life, Max = life, Size = radius, Col = col, Kind = 3 }, 0f));
    }

    public void Text(Vector2 pos, string text, Color col, int size = 11, float life = 0.8f)
    {
        Rec(6)?.Vec(pos).Str(text).Col(col).Byte((byte)Math.Clamp(size, 0, 255)).Half(life);
        _texts.Add(new FloatText { Pos = pos + new Vector2(G.Range(-6, 6), 0), Text = text, Col = col, Life = life, Max = life, Size = size });
    }

    public void AddShake(float amount) => Shake = Math.Min(14f, Shake + amount);

    private void Add(Vector2 pos, Vector2 vel, float life, float size, Color col, int kind, float grav = 0, float drag = 1.5f, float spin = 0, float depthSpread = 0f)
        => _parts.Add(Deep(new Particle { Pos = pos, Vel = vel, Life = life, Max = life, Size = size, Col = col, Kind = kind, Grav = grav, Drag = drag, Rot = G.Range(0, Mathf.Tau), Spin = spin }, depthSpread));

    /// <summary>A small hit spark in a random direction.</summary>
    public void Spark(Vector2 pos, Color col) => Spark(pos, G.RandDir(), false, col);

    /// <summary>A bright disc that blooms and fades in a blink.</summary>
    public void Flash(Vector2 pos, float radius, Color col, float life = 0.14f)
    {
        Rec(7)?.Vec(pos).Half(radius).Col(col).Half(life);
        Add(pos, Vector2.Zero, life, radius, col, 7);
        LightUp(pos, col, 1.5f + radius * 0.05f, radius * 3f, life * 1.6f);
    }

    /// <summary>A flattened ring racing outward along the ground.</summary>
    public void Shockwave(Vector2 pos, float radius, Color col, float life = 0.35f)
    {
        Rec(8)?.Vec(pos).Half(radius).Col(col).Half(life);
        Add(pos, Vector2.Zero, life, radius, col, 8);
    }

    /// <summary>Puffs of dust at the feet (landings, skids, charges).</summary>
    public void Dust(Vector2 pos, int n, float spread = 1f, Color? col = null)
    {
        var o = Rec(9)?.Vec(pos).Byte((byte)Math.Clamp(n, 0, 255)).Half(spread).Bool(col.HasValue);
        if (o != null && col.HasValue) o.Col(col.Value);
        var c = col ?? new Color(0.7f, 0.64f, 0.56f, 0.55f);
        for (int k = 0; k < n; k++)
            Add(pos + new Vector2(G.Range(-6, 6) * spread, G.Range(-2, 1)), new Vector2(G.Range(-50, 50) * spread, G.Range(-30, -8)), G.Range(0.35f, 0.6f), G.Range(3f, 5.5f), c, 9, -20, 3f, 0, 1.2f * spread);
    }

    public void Smoke(Vector2 pos, int n, Color col, float speed = 40f)
    {
        Rec(10)?.Vec(pos).Byte((byte)Math.Clamp(n, 0, 255)).Col(col).Half(speed);
        for (int k = 0; k < n; k++)
            Add(pos + G.RandDir() * 6, G.RandDir() * speed * G.Range(0.3f, 1f) + new Vector2(0, -20), G.Range(0.6f, 1.1f), G.Range(5, 9), col, 9, -30, 2f, 0, speed / 16f * 0.5f);
    }

    /// <summary>Tumbling chunks of rock (or bone, or crystal).</summary>
    public void Debris(Vector2 pos, Color col, int n, float speed = 220f)
    {
        Rec(11)?.Vec(pos).Col(col).Byte((byte)Math.Clamp(n, 0, 255)).Half(speed);
        for (int k = 0; k < n; k++)
            Add(pos, G.RandDir() * speed * G.Range(0.4f, 1f) + new Vector2(0, -80), G.Range(0.5f, 0.9f), G.Range(1.8f, 3.4f), col, 10, 700, 0.6f, G.Range(-14, 14), speed / 16f * 0.45f);
    }

    /// <summary>A fan of droplets thrown up where something hits the water (or lava).</summary>
    public void Splash(Vector2 pos, float strength, Color col)
    {
        Rec(12)?.Vec(pos).Half(strength).Col(col);
        int n = (int)(6 + strength * 14);
        for (int k = 0; k < n; k++)
        {
            float a = -Mathf.Pi / 2 + G.Range(-0.9f, 0.9f);
            Add(pos, new Vector2(MathF.Cos(a), MathF.Sin(a)) * G.Range(80, 180 + strength * 160), G.Range(0.4f, 0.8f), G.Range(1.4f, 2.6f), col, 11, 650, 0.4f, 0, 3f + strength * 3f);
        }
        _quiet++;
        Shockwave(pos, 16 + strength * 22, new Color(col, 0.7f), 0.4f);
        _quiet--;
    }

    /// <summary>A four-point star twinkle (sheens on treasure, crystals, curling shardlings).</summary>
    public void Glint(Vector2 pos, Color col, float size = 7f)
    {
        Rec(13)?.Vec(pos).Col(col).Half(size);
        Add(pos, Vector2.Zero, 0.35f, size, col, 12, 0, 0, G.Range(-3, 3));
    }

    /// <summary>A glowing mote that drifts upward and flickers out.</summary>
    public void Ember(Vector2 pos, Color col)
    {
        Rec(14)?.Vec(pos).Col(col);
        Add(pos, new Vector2(G.Range(-20, 20), G.Range(-70, -30)), G.Range(0.5f, 1.1f), G.Range(1.2f, 2.2f), col, 13, -20, 0.8f, 0, 1.2f);
    }

    /// <summary>A single fading mote left behind by something moving fast.</summary>
    public void Trail(Vector2 pos, Color col)
    {
        Rec(15)?.Vec(pos).Col(col);
        Add(pos, Vector2.Zero, 0.25f, 3f, col, 0, 0);
    }

    /// <summary>A faint mote carried on a draught from one spot to another, sinking away from the viewer (air drawn down an exit).</summary>
    public void Mote(Vector2 from, Vector2 to, Color col)
    {
        Rec(16)?.Vec(from).Vec(to).Col(col);
        float life = G.Range(1f, 1.6f);
        var p = Deep(new Particle { Pos = from, Vel = (to - from) / life, Life = life, Max = life, Size = G.Range(1f, 1.7f), Col = col, Kind = 0 }, 0f);
        p.ZVel = -0.9f;
        _parts.Add(p);
    }

    /// <summary>Motes drawn in from a ring around a point, reaching it as they fade (something being seized from within).</summary>
    public void Converge(Vector2 center, float radius, Color col, int n, float life)
    {
        Rec(17)?.Vec(center).Half(radius).Col(col).Byte((byte)Math.Clamp(n, 0, 255)).Half(life);
        life = Math.Max(0.05f, life);
        for (int k = 0; k < n; k++)
        {
            var from = center + G.RandDir() * radius * G.Range(0.7f, 1.1f);
            float l = life * G.Range(0.75f, 1f);
            var p = Deep(new Particle { Pos = from, Vel = (center - from) / l, Life = l, Max = l, Size = G.Range(1.4f, 2.4f), Col = col, Kind = 13, Rot = G.Range(0, Mathf.Tau) }, 0f);
            p.ZVel = (0.35f - p.Z) / l;
            _parts.Add(p);
        }
    }

    /// <summary>A stream of light from one point to another (a heal reaching an ally).</summary>
    public void Beam(Vector2 from, Vector2 to, Color col)
    {
        Rec(18)?.Vec(from).Vec(to).Col(col);
        float len = from.DistanceTo(to);
        int n = Math.Clamp((int)(len / 7f), 3, 60);
        var dir = len > 0.01f ? (to - from) / len : Vector2.Right;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n;
            // motes drift along toward the target, the ones near it brightest
            Add(from.Lerp(to, t) + G.RandDir() * 1.5f, dir * 60f, 0.2f + 0.25f * t, 2.2f + 1.2f * t, col, 0, 0, 2f);
        }
        LightUp(to, col, 2f, 70f, 0.25f);
    }

    /// <summary>A crescent of air where a big blow sweeps past (dir = +1 right, -1 left).</summary>
    public void Swoosh(Vector2 pos, float dir, float radius, Color col)
    {
        Rec(19)?.Vec(pos).Half(dir).Half(radius).Col(col);
        Add(pos, new Vector2(dir, 0), 0.18f, radius, col, 14);
    }

    /// <summary>Enemy death: a pop of light, a ring and a scatter of bits.</summary>
    public void Pop(Vector2 pos, Color col, float radius)
    {
        Rec(20)?.Vec(pos).Col(col).Half(radius);
        _quiet++;
        LightUp(pos, col, 4f, radius * 6f + 60f, 0.3f);
        Flash(pos, radius + 8, new Color(1f, 0.95f, 0.85f), 0.12f);
        Ring(pos, radius + 4, new Color(col, 0.8f), 0.25f);
        Burst(pos, col, 8, 150, 2.2f, 0.45f);
        Burst(pos, new Color(1, 1, 1, 0.9f), 4, 110, 1.6f, 0.25f, 0);
        _quiet--;
    }

    /// <summary>The big one: flash, shockwave, sparks, embers, smoke and debris.</summary>
    public void Explosion(Vector2 pos, Color col, float scale = 1f)
    {
        Rec(21)?.Vec(pos).Col(col).Half(scale);
        _quiet++;
        LightUp(pos, new Color(1f, 0.75f, 0.45f), 10f * scale, 220f * scale, 0.5f);
        Flash(pos, 34 * scale, new Color(1f, 0.95f, 0.8f), 0.18f);
        Shockwave(pos, 60 * scale, new Color(1f, 0.9f, 0.7f, 0.9f), 0.4f);
        Ring(pos, 30 * scale, new Color(col, 0.9f), 0.35f);
        for (int k = 0; k < 10 * scale; k++) Spark(pos, G.RandDir(), true, col);
        for (int k = 0; k < 8 * scale; k++) Ember(pos + G.RandDir() * 10, new Color(1f, 0.7f, 0.3f));
        Smoke(pos, (int)(6 * scale), new Color(0.3f, 0.28f, 0.26f, 0.5f), 70 * scale);
        Debris(pos, col.Darkened(0.3f), (int)(8 * scale), 260 * scale);
        AddShake(5 * scale);
        _quiet--;
    }

    private Color _screenCol;
    private float _screenT, _screenMax;

    /// <summary>Washes the whole view with a colour for a moment (level-ups, enrages, potions).</summary>
    public void ScreenFlash(Color col, float seconds = 0.25f) { _screenCol = col; _screenT = _screenMax = seconds; }
    /// <summary>The screen wash right now (alpha already faded).</summary>
    public Color ScreenWash => _screenT > 0 ? new Color(_screenCol, _screenCol.A * 0.45f * (_screenT / _screenMax)) : new Color(0, 0, 0, 0);

    public void Clear() { _parts.Clear(); _texts.Clear(); Flashes.Clear(); Shake = 0; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var cave = G.Cave;
        for (int k = _parts.Count - 1; k >= 0; k--)
        {
            var p = _parts[k];
            p.Life -= dt;
            if (p.Life <= 0) { _parts.RemoveAt(k); continue; }
            p.Vel.Y += p.Grav * dt;
            p.Vel *= 1f / (1f + p.Drag * dt);
            p.Pos += p.Vel * dt;
            p.ZVel *= 1f / (1f + p.Drag * dt);
            p.Z = Math.Clamp(p.Z + p.ZVel * dt, -1.2f, 2.5f);
            p.Rot += p.Spin * dt;
            if (p.Kind == 10 && cave != null && cave.IsSolid(p.Pos)) { p.Vel = new Vector2(p.Vel.X * 0.5f, -p.Vel.Y * 0.35f); p.Pos -= new Vector2(0, 1); }
            if (p.Kind == 2 && cave != null && p.Pos.Y < cave.WaterY) p.Life = 0;
            _parts[k] = p;
        }
        for (int k = _texts.Count - 1; k >= 0; k--)
        {
            var t = _texts[k];
            t.Life -= dt;
            t.Pos.Y -= 28 * dt;
            if (t.Life <= 0) _texts.RemoveAt(k); else _texts[k] = t;
        }
        Shake = Math.Max(0, Shake - dt * 30f);
        _screenT = Math.Max(0, _screenT - dt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var p in _parts)
        {
            float a = Math.Clamp(p.Life / p.Max, 0, 1);
            var c = new Color(p.Col, p.Col.A * a);
            switch (p.Kind)
            {
                case 0: DrawCircle(p.Pos, p.Size * (0.4f + 0.6f * a), c); break;
                case 1: DrawLine(p.Pos, p.Pos - p.Vel * 0.03f, c, p.Size); break;
                case 2: DrawArc(p.Pos, p.Size, 0, Mathf.Tau, 8, c, 1f); break;
                case 3: DrawArc(p.Pos, p.Size * (1.5f - a * 0.5f), 0, Mathf.Tau, 24, c, 2f * a + 0.5f); break;
                case 5: DrawLine(p.Pos - p.Vel * 0.045f, p.Pos, new Color(1, 1, 1, a), p.Size * a + 0.5f); DrawLine(p.Pos - p.Vel * 0.03f, p.Pos, c, p.Size * a * 0.6f + 0.3f); break;
                case 6:
                    DrawCircle(p.Pos, p.Size * (1.2f - a * 0.4f), new Color(p.Col, 0.35f * a));
                    DrawCircle(p.Pos, p.Size * 0.55f * a, new Color(1, 1, 1, 0.9f * a));
                    break;
                case 7:
                {
                    float r = p.Size * (1.25f - 0.5f * a);
                    DrawCircle(p.Pos, r, new Color(p.Col, 0.3f * a));
                    DrawCircle(p.Pos, r * 0.6f, new Color(p.Col, 0.55f * a));
                    DrawCircle(p.Pos, r * 0.3f * a, new Color(1, 1, 1, 0.9f * a));
                    break;
                }
                case 8:
                {
                    float r = p.Size * (1.1f - a);
                    DrawSetTransform(p.Pos, 0, new Vector2(1, 0.32f));
                    DrawArc(Vector2.Zero, Math.Max(1, r), 0, Mathf.Tau, 32, c, 5f * a + 1f);
                    DrawArc(Vector2.Zero, Math.Max(1, r * 0.85f), 0, Mathf.Tau, 32, new Color(1, 1, 1, 0.4f * a), 2f * a + 0.5f);
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    break;
                }
                case 9: DrawCircle(p.Pos, p.Size * (1.6f - 0.6f * a), new Color(p.Col, p.Col.A * a * 0.8f)); break;
                case 10:
                {
                    var ax = new Vector2(MathF.Cos(p.Rot), MathF.Sin(p.Rot)) * p.Size;
                    var ay = new Vector2(-ax.Y, ax.X) * 0.7f;
                    DrawColoredPolygon(new[] { p.Pos + ax, p.Pos + ay, p.Pos - ax, p.Pos - ay }, new Color(p.Col, Math.Min(1, a * 2)));
                    break;
                }
                case 11: DrawLine(p.Pos, p.Pos - p.Vel * 0.02f, c, p.Size); DrawCircle(p.Pos, p.Size * 0.6f, c); break;
                case 12:
                {
                    float s = p.Size * MathF.Sin(a * Mathf.Pi);
                    var d1 = new Vector2(MathF.Cos(p.Rot), MathF.Sin(p.Rot)) * s;
                    var d2 = new Vector2(-d1.Y, d1.X);
                    DrawLine(p.Pos - d1, p.Pos + d1, new Color(1, 1, 1, a), 1.4f);
                    DrawLine(p.Pos - d2 * 0.6f, p.Pos + d2 * 0.6f, new Color(1, 1, 1, a), 1.2f);
                    DrawCircle(p.Pos, s * 0.35f, new Color(p.Col, 0.7f * a));
                    break;
                }
                case 13:
                {
                    float fl = 0.6f + 0.4f * MathF.Sin(p.Life * 40 + p.Rot * 5);
                    DrawCircle(p.Pos, p.Size * 2.2f, new Color(p.Col, 0.18f * a * fl));
                    DrawCircle(p.Pos, p.Size, new Color(p.Col.Lightened(0.3f), a * fl));
                    break;
                }
                case 14:
                {
                    // a thick crescent sweeping from above to below on the facing side
                    float dir = p.Vel.X >= 0 ? 1 : -1;
                    float start = dir > 0 ? -1.3f : Mathf.Pi - 1.3f;
                    float sweep = 2.6f * (1.15f - a * 0.4f);
                    float from = dir > 0 ? start : Mathf.Pi + 1.3f - sweep;
                    DrawArc(p.Pos, p.Size, from, from + sweep, 18, new Color(p.Col, p.Col.A * a), 6f * a + 1f);
                    DrawArc(p.Pos, p.Size * 0.88f, from + 0.2f, from + sweep - 0.2f, 16, new Color(1, 1, 1, 0.6f * a), 2f * a + 0.5f);
                    break;
                }
            }
        }
        if (_screenT > 0)
        {
            var inv = GetViewport().GetCanvasTransform().AffineInverse();
            var size = GetViewport().GetVisibleRect().Size;
            var tl = inv * Vector2.Zero; var br = inv * size;
            DrawRect(new Rect2(tl - new Vector2(40, 40), br - tl + new Vector2(80, 80)), new Color(_screenCol, _screenCol.A * 0.45f * (_screenT / _screenMax)));
        }
        var font = ThemeDB.FallbackFont;
        foreach (var t in _texts)
        {
            float a = Math.Clamp(t.Life / t.Max * 2f, 0, 1);
            float age = t.Max - t.Life;
            float pop = age < 0.1f ? 1.7f - age * 7f : 1f;
            int size = Math.Max(6, (int)(t.Size * pop));
            var pos = t.Pos - new Vector2(t.Text.Length * size * 0.28f, 0);
            DrawString(font, pos + new Vector2(1, 1), t.Text, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, a * 0.8f));
            DrawString(font, pos, t.Text, HorizontalAlignment.Left, -1, size, new Color(t.Col, a));
        }
    }
}
