using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>A light the level would like to have (a glowing mushroom, crystal, lava seam...).</summary>
public sealed class LightSpot
{
    public Vector3 Pos;
    public Color Col;
    public float Energy = 1f, Range = 5f;
    /// <summary>0 = steady, 1 = flame-like flicker.</summary>
    public float Flicker;
    /// <summary>How much it lights the volumetric fog.</summary>
    public float Fog = 0.5f;
    public float Phase;
}

/// <summary>
/// A level can have hundreds of glowing things; only the few dozen nearest the camera get a real
/// light at any time. Lights fade in and out at the edge of the view so nothing pops.
/// </summary>
public partial class LightPool3D : Node3D
{
    private const int PoolSize = 40;
    private const float Reach = 34f;

    private readonly List<LightSpot> _spots = new();
    private readonly OmniLight3D[] _lights = new OmniLight3D[PoolSize];
    private readonly LightSpot[] _assigned = new LightSpot[PoolSize];
    private readonly float[] _fade = new float[PoolSize];
    private readonly HashSet<LightSpot> _wanted = new();
    private readonly List<(float d, LightSpot s)> _near = new();
    private float _pickT, _t;

    public int Count => _spots.Count;

    public override void _Ready()
    {
        for (int k = 0; k < PoolSize; k++)
        {
            _lights[k] = new OmniLight3D { Visible = false, ShadowEnabled = false, OmniAttenuation = 1.6f };
            AddChild(_lights[k]);
        }
    }

    public void Add(LightSpot s)
    {
        s.Phase = (float)(s.Pos.X * 1.31 + s.Pos.Y * 0.77) % 6.283f;
        _spots.Add(s);
    }

    public void Clear()
    {
        _spots.Clear();
        for (int k = 0; k < PoolSize; k++) { _assigned[k] = null; _fade[k] = 0; _lights[k].Visible = false; }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;
        // the point on the play plane the camera looks at
        var f = cam.GlobalPosition;
        var fwd = -cam.GlobalBasis.Z;
        var focus = fwd.Z < -0.01f ? f + fwd * (-f.Z / fwd.Z) : new Vector3(f.X, f.Y, 0);

        _pickT -= dt;
        if (_pickT <= 0f)
        {
            _pickT = 0.2f;
            _near.Clear();
            foreach (var s in _spots)
            {
                float dx = s.Pos.X - focus.X, dy = (s.Pos.Y - focus.Y) * 1.5f;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < Reach + s.Range) _near.Add((d - s.Range * 0.5f, s));
            }
            _near.Sort((a, b) => a.d.CompareTo(b.d));
            _wanted.Clear();
            for (int k = 0; k < _near.Count && k < PoolSize; k++) _wanted.Add(_near[k].s);
            // keep lights that are still wanted; hand free slots to new ones
            for (int k = 0; k < PoolSize; k++)
                if (_assigned[k] != null && !_wanted.Contains(_assigned[k]) && _fade[k] <= 0f) _assigned[k] = null;
            foreach (var s in _wanted)
            {
                if (Array.IndexOf(_assigned, s) >= 0) continue;
                int free = Array.IndexOf(_assigned, null);
                if (free < 0) break;
                _assigned[free] = s;
                _fade[free] = 0f;
            }
        }

        for (int k = 0; k < PoolSize; k++)
        {
            var s = _assigned[k];
            var l = _lights[k];
            if (s == null) { l.Visible = false; continue; }
            bool want = _wanted.Contains(s);
            _fade[k] = Math.Clamp(_fade[k] + (want ? dt : -dt) * 2.5f, 0f, 1f);
            if (_fade[k] <= 0f && !want) { _assigned[k] = null; l.Visible = false; continue; }
            float dx = s.Pos.X - focus.X, dy = (s.Pos.Y - focus.Y) * 1.5f;
            float edge = 1f - W3.SmoothStep(Reach * 0.75f, Reach + s.Range, MathF.Sqrt(dx * dx + dy * dy));
            float flick = s.Flicker > 0f
                ? 1f - s.Flicker * (0.18f + 0.12f * MathF.Sin(_t * 13f + s.Phase) + 0.08f * MathF.Sin(_t * 31f + s.Phase * 2f))
                : 1f;
            l.Visible = true;
            l.Position = s.Pos;
            l.LightColor = s.Col;
            l.OmniRange = s.Range;
            l.LightEnergy = s.Energy * _fade[k] * edge * flick;
            l.LightVolumetricFogEnergy = s.Fog;
        }
    }
}
