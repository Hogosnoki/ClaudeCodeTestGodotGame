using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// --scenario=NAME [--shots=DIR]: small staged situations that check one behaviour and print
/// ok / FAIL lines, then PASS or FAIL (screenshots along the way with --shots):
///  * water: a spider and a bear dropped into the water beside the hero must swim after it and
///    attack there (the spider darts, the bear bites).
///  * fossilcam: in the Fossil Graveyards, the camera must start on the hero and keep it in view
///    as it walks one way, then the other.
/// </summary>
public partial class Main
{
    private string _scenario = "";
    private float _scT;
    private bool _scOk = true, _scDone;
    private PlayerInput _scInput;
    private readonly HashSet<string> _scShots = new();
    private readonly Dictionary<Enemy, (float start, float closest, bool attacked)> _scWatch = new();
    private float _scHp;

    private void ScCheck(string what, bool ok)
    {
        GD.Print($"[scenario] {(ok ? "ok  " : "FAIL")} {what}");
        _scOk &= ok;
    }

    private void ScShot(string name)
    {
        if (_shotDir == "" || !_scShots.Add(name)) return;
        GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/{name}.png");
    }

    private void ScEnd()
    {
        if (_scDone) return;
        _scDone = true;
        GD.Print(_scOk ? $"[scenario] {_scenario} PASS" : $"[scenario] {_scenario} FAIL");
        SafeQuit.Request(this, _scOk ? 0 : 1);
    }

    /// <summary>The biome a scenario needs.</summary>
    private string ScenarioBiome => _scenario switch
    {
        "water" => "slime",
        "fossilcam" => "fossils",
        _ => null,
    };

    private void BeginScenario()
    {
        StartPlaying();
        _hud.HintTime = 0;
        G.Player.InputOverride = () => _scInput;
        GD.Print($"[scenario] {_scenario} in the {G.Biome.Name}");
    }

    private void ScenarioTick(float dt)
    {
        if (_scDone || _state != State.Playing) return;
        _scT += dt;
        switch (_scenario)
        {
            case "water": WaterScenario(); break;
            case "fossilcam": CameraScenario(); break;
            default: ScCheck($"a scenario called '{_scenario}'", false); ScEnd(); break;
        }
    }

    // ---------------------------------------------------------------- the camera follows the hero

    private float _scWorst, _scLogT;

    private void CameraScenario()
    {
        var p = G.Player;
        var half = ViewHalf(0);
        var center = _cam.GetScreenCenterPosition();
        var off = center - p.GlobalPosition;
        float frac = Math.Max(Math.Abs(off.X) / half.X, Math.Abs(off.Y) / half.Y);
        if (_scT > 1.2f) _scWorst = Math.Max(_scWorst, frac);
        _scInput = new PlayerInput { Move = new Vector2(_scT < 5f ? 1 : -1, 0), Jump = G.Chance(0.02f), JumpHeld = true };
        _scLogT -= 1f / 60f;
        if (_scLogT <= 0)
        {
            _scLogT = 0.5f;
            GD.Print($"[scenario] t={_scT:0.0} hero {p.GlobalPosition.Round()} camera {_cam.GlobalPosition.Round()} centre {center.Round()} off {off.Round()} ({frac:0.00} of the half view) limits {_cam.LimitLeft},{_cam.LimitTop}..{_cam.LimitRight},{_cam.LimitBottom} start {G.Cave.StartPos.Round()} boss {(ActiveBoss != null ? ActiveBoss.GlobalPosition.Round().ToString() : "-")}");
        }
        if (_scT > 0.3f) ScShot("cam_0");
        if (_scT > 2f) ScShot("cam_1");
        if (_scT > 4.5f) ScShot("cam_2");
        if (_scT > 7f) ScShot("cam_3");
        if (_scT < 7.2f) return;
        ScCheck($"the camera keeps the hero near the middle of the view (at worst {_scWorst:0.00} of the way to the edge)", _scWorst < 0.6f);
        ScEnd();
    }

    // ---------------------------------------------------------------- water

    private void WaterScenario()
    {
        var p = G.Player;
        var cave = G.Cave;
        if (_scWatch.Count == 0)
        {
            if (_scT < 0.3f) return;
            // deep, open water with room around it
            Vector2? spot = null;
            foreach (var sp in cave.Spawns.Where(s => s.Kind == SpawnKind.Water).OrderByDescending(s => s.Pos.Y))
            {
                bool open = true;
                for (int dx = -150; dx <= 150 && open; dx += 25)
                    for (int dy = -40; dy <= 40 && open; dy += 20)
                        open = cave.IsWater(sp.Pos + new Vector2(dx, dy)) && !cave.IsSolid(sp.Pos + new Vector2(dx, dy));
                if (open) { spot = sp.Pos; break; }
            }
            if (spot == null) { ScCheck("open water to test in", false); ScEnd(); return; }
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.GlobalPosition = spot.Value;
            p.Velocity = Vector2.Zero;
            p.Stats.MaxHp = 500; p.Hp = 500;
            _scHp = p.Hp;
            var spider = new Spider { Position = spot.Value + new Vector2(110, -10) };
            var bear = new Bear { Position = spot.Value + new Vector2(-120, -10) };
            foreach (var e in new Enemy[] { spider, bear })
            {
                e.SetMeta("test", true);
                _world.AddChild(e);
                e.Wake();
                e.Engage();
                // (the test spider comes off the ceiling: it starts in the water, on its own feet)
                if (e is Spider s) s.ForceGround();
                _scWatch[e] = (e.GlobalPosition.DistanceTo(p.GlobalPosition), float.MaxValue, false);
            }
            _scT = 0;
            return;
        }
        // hold the hero where it is (it would sink slowly otherwise)
        p.Velocity = Vector2.Zero;
        foreach (var e in _scWatch.Keys.ToList())
        {
            if (!IsInstanceValid(e)) continue;
            var w = _scWatch[e];
            float d = e.GlobalPosition.DistanceTo(p.GlobalPosition);
            _scWatch[e] = (w.start, Math.Min(w.closest, d), w.attacked || e.Attacking);
        }
        if (_scT > 1.2f) ScShot("water_1");
        if (_scT > 2.4f) ScShot("water_2");
        if (_scT > 3.6f) ScShot("water_3");
        if (_scT < 6f) return;
        foreach (var (e, w) in _scWatch)
        {
            string name = e.GetType().Name.ToLowerInvariant();
            bool inWater = IsInstanceValid(e) && G.Cave.IsWater(e.GlobalPosition);
            ScCheck($"the {name} stays in the water and swims up to the hero ({w.start:0} px -> closest {w.closest:0} px, in water {inWater})", w.closest < 45f * e.Size && inWater);
            ScCheck($"and attacks it there (attacking seen: {w.attacked})", w.attacked);
        }
        ScCheck($"the hero was hurt in the water (hp {_scHp:0} -> {p.Hp:0})", p.Hp < _scHp);
        ScEnd();
    }
}
