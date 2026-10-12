using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Ambient light only (Settings > Graphics, on by default while the look is being settled): every light in the game's views (the
/// directional key, fill and rim lights, the torches, spell and fire lights, the camp's sun) is cut off from every surface, so nothing is
/// shaded by direction and the shapes and edges read by their colours and ink borders alone. The views' ambient light makes up for
/// it: in the caves near white and equally strong everywhere (DebugTuning "amb_tint" and "amb_energy", Stage3D.ApplySettings), the same at
/// the camp (<see cref="CampEnergy"/>, CampScene). Metal, with nothing lit to mirror, mostly shows its own colour (the shader global
/// light_ambient_only, in the creature shader). Lights are left in place, only muted: a light's cull
/// mask goes to 0 (it reaches nothing), its shadow and its glow in the fog go off, and switching the setting off gives each back what
/// it had. The lights are checked every frame, since the game turns some of them up and down as it goes. (The menu portraits, in their own
/// viewports, keep their lights.)
/// </summary>
public partial class AmbientOnly : Node
{
    /// <summary>The camp's ambient light while the sun is off (near white, not the sky's).</summary>
    public const float CampEnergy = 1.1f;
    // (the cave's ambient strength and biome tint while the other lights are off are debug-tunable: DebugTuning "amb_energy", "amb_tint")

    private readonly HashSet<Light3D> _lights = new();
    private static readonly List<(WeakRef m, float metallic)> Metals = new();

    /// <summary>A metal material: with ambient light only it has nothing lit to mirror, so it is drawn mostly as its own colour (its
    /// metallic turned down, and back up when the setting is off). Returns the material.</summary>
    public static StandardMaterial3D Metal(StandardMaterial3D m)
    {
        Metals.Add((GodotObject.WeakRef(m), m.Metallic));
        m.Metallic = Metallic(m.Metallic);
        return m;
    }

    private static float Metallic(float full) => GameSettings.AmbientOnly ? full * 0.15f : full;

    private static void RefreshMetals()
    {
        Metals.RemoveAll(e => e.m.GetRef().Obj is not StandardMaterial3D);
        foreach (var (w, full) in Metals) ((StandardMaterial3D)w.GetRef().Obj).Metallic = Metallic(full);
    }
    private readonly Dictionary<Light3D, (uint mask, bool shadow, float fog)> _saved = new();
    private bool _was;

    public override void _Ready()
    {
        Name = "AmbientOnly";
        ProcessMode = ProcessModeEnum.Always;
        GetTree().NodeAdded += OnAdded;
        foreach (var n in GetTree().Root.FindChildren("*", "Light3D", true, false)) OnAdded(n);
    }

    public override void _ExitTree() => GetTree().NodeAdded -= OnAdded;

    private void OnAdded(Node n)
    {
        // (only the game's own views, the cave and the camp: a menu portrait's lights keep their job)
        if (n is Light3D l && n.GetViewport() is not HeroPortrait) _lights.Add(l);
    }

    public override void _Process(double delta)
    {
        bool on = GameSettings.AmbientOnly;
        if (on != _was) RefreshMetals();
        _lights.RemoveWhere(l => !IsInstanceValid(l) || !l.IsInsideTree());
        foreach (var k in new List<Light3D>(_saved.Keys)) if (!IsInstanceValid(k)) _saved.Remove(k);
        if (on)
        {
            foreach (var l in _lights)
            {
                if (!_saved.ContainsKey(l)) _saved[l] = (l.LightCullMask, l.ShadowEnabled, l.LightVolumetricFogEnergy);
                // (the game may have set the fog glow again since the last frame: kept, to give back later)
                if (l.LightVolumetricFogEnergy > 0f) _saved[l] = (_saved[l].mask, _saved[l].shadow, l.LightVolumetricFogEnergy);
                if (l.LightCullMask != 0) l.LightCullMask = 0;
                if (l.ShadowEnabled) l.ShadowEnabled = false;
                if (l.LightVolumetricFogEnergy != 0f) l.LightVolumetricFogEnergy = 0f;
            }
        }
        else if (_was)
        {
            foreach (var (l, s) in _saved)
            {
                l.LightCullMask = s.mask;
                l.ShadowEnabled = s.shadow;
                l.LightVolumetricFogEnergy = s.fog;
            }
            _saved.Clear();
        }
        _was = on;
    }
}
