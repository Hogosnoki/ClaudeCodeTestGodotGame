using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>Lightweight particle / floating text / screen-shake system drawn from one node.</summary>
public partial class FxLayer : Node2D
{
    private struct Particle
    {
        public Vector2 Pos, Vel;
        public float Life, Max, Size, Grav, Drag;
        public Color Col;
        public int Kind; // 0 dot, 1 streak, 2 bubble, 3 ring, 4 ghost
        public float Extra;
    }

    private struct FloatText
    {
        public Vector2 Pos;
        public string Text;
        public Color Col;
        public float Life, Max;
        public int Size;
    }

    private readonly List<Particle> _parts = new(1024);
    private readonly List<FloatText> _texts = new();
    public float Shake;

    public override void _Ready() { ZIndex = 12; }

    public void Burst(Vector2 pos, Color col, int n, float speed, float size = 2.5f, float life = 0.5f, float grav = 300f, int kind = 0, float drag = 1.5f)
    {
        for (int k = 0; k < n; k++)
        {
            var d = G.RandDir() * speed * G.Range(0.3f, 1f);
            _parts.Add(new Particle { Pos = pos, Vel = d, Life = life * G.Range(0.6f, 1.1f), Max = life, Size = size * G.Range(0.6f, 1.3f), Grav = grav, Drag = drag, Col = col, Kind = kind });
        }
    }

    public void Directional(Vector2 pos, Vector2 dir, float spread, Color col, int n, float speed, float size = 2f, float life = 0.4f, float grav = 200f, int kind = 1)
    {
        for (int k = 0; k < n; k++)
        {
            var d = dir.Rotated(G.Range(-spread, spread)) * speed * G.Range(0.4f, 1f);
            _parts.Add(new Particle { Pos = pos, Vel = d, Life = life * G.Range(0.6f, 1.1f), Max = life, Size = size, Grav = grav, Drag = 2f, Col = col, Kind = kind });
        }
    }

    public void Bubbles(Vector2 pos, int n)
    {
        for (int k = 0; k < n; k++)
            _parts.Add(new Particle { Pos = pos + G.RandDir() * 4, Vel = new Vector2(G.Range(-15, 15), G.Range(-60, -20)), Life = G.Range(0.6f, 1.4f), Max = 1.4f, Size = G.Range(1.5f, 3.5f), Grav = -60, Drag = 1f, Col = new Color(0.75f, 0.95f, 1f, 0.8f), Kind = 2 });
    }

    public void Ring(Vector2 pos, float radius, Color col, float life = 0.3f)
        => _parts.Add(new Particle { Pos = pos, Life = life, Max = life, Size = radius, Col = col, Kind = 3 });

    /// <summary>Dodge afterimage of the player.</summary>
    public void Ghost(Vector2 pos, float facing)
        => _parts.Add(new Particle { Pos = pos, Life = 0.25f, Max = 0.25f, Size = 1, Col = new Color(0.5f, 0.85f, 1f, 0.5f), Kind = 4, Extra = facing });

    public void Text(Vector2 pos, string text, Color col, int size = 11, float life = 0.8f)
        => _texts.Add(new FloatText { Pos = pos + new Vector2(G.Range(-6, 6), 0), Text = text, Col = col, Life = life, Max = life, Size = size });

    public void AddShake(float amount) => Shake = Math.Min(14f, Shake + amount);

    public void Clear() { _parts.Clear(); _texts.Clear(); Shake = 0; }

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
                case 4:
                    // player silhouette (cloak, hood, trailing scarf)
                    DrawSetTransform(p.Pos, 0, new Vector2(p.Extra, 1));
                    DrawColoredPolygon(new[] { new Vector2(-4, -6), new Vector2(4, -6), new Vector2(5, 6), new Vector2(-7, 8) }, c);
                    DrawCircle(new Vector2(0.5f, -10.5f), 5.5f, c);
                    DrawLine(new Vector2(-2, -8), new Vector2(-14, -6), c, 2f);
                    DrawLine(new Vector2(0, 5), new Vector2(-2, 13), c, 2.5f);
                    DrawLine(new Vector2(0, 5), new Vector2(3, 13), c, 2.5f);
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    break;
            }
        }
        var font = ThemeDB.FallbackFont;
        foreach (var t in _texts)
        {
            float a = Math.Clamp(t.Life / t.Max * 2f, 0, 1);
            var pos = t.Pos - new Vector2(t.Text.Length * t.Size * 0.28f, 0);
            DrawString(font, pos + new Vector2(1, 1), t.Text, HorizontalAlignment.Left, -1, t.Size, new Color(0, 0, 0, a * 0.8f));
            DrawString(font, pos, t.Text, HorizontalAlignment.Left, -1, t.Size, new Color(t.Col, a));
        }
    }
}
