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
///  * magma: the Magma Caverns hide chests in the lava; lava throws a hero out, but with Magma
///    Skin they swim in it (burned for 30%), swim up out of it, and open a chest down there.
///  * vault: keys and the vault's gate (see Main.VaultTest.cs).
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
        "mouth" => "entrance",
        "drain" => "entrance",
        "motion" => "entrance",
        "elementals" => "slime",
        "affinity" => "entrance",
        "magma" => "magma",
        "vault" => "den",
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
        // (the cave mouth's scenario carries on after the run ends, at the title)
        // (the magma scenario opens a chest down in the lava and takes a card)
        if (_scDone || (_state != State.Playing && _scenario != "mouth" && !(_scenario is "magma" or "vault" && _upgradeMenu.Visible))) return;
        _scT += dt;
        switch (_scenario)
        {
            case "water": WaterScenario(); break;
            case "fossilcam": CameraScenario(); break;
            case "mouth": MouthScenario(); break;
            case "drain": DrainScenario(); break;
            case "motion": MotionScenario(); break;
            case "elementals": ElementalsScenario(); break;
            case "affinity": AffinityScenario(); break;
            case "magma": MagmaScenario(); break;
            case "vault": VaultScenario(); break;
            default: ScCheck($"a scenario called '{_scenario}'", false); ScEnd(); break;
        }
    }

    // ---------------------------------------------------------------- the drain's burst (--hero=vitalist)

    private Enemy _scFoe;
    private int _scFrame = -1;

    /// <summary>One drain at a golem, a frame-by-frame record of the cast and the burst (drain_N.png).</summary>
    private void DrainScenario()
    {
        var p = G.Player;
        if (_scFoe == null)
        {
            if (_scT < 0.5f) return;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            _scFoe = new Golem { Position = p.GlobalPosition + new Vector2(110, -20) };
            _scFoe.SetMeta("test", true);
            _world.AddChild(_scFoe);
            _scFoe.Wake();
            _scT = 0;
            return;
        }
        if (_scFrame < 0)
        {
            _scFoe.Freeze(0.3f, hold: true);
            if (_scT < 1.2f) return;
            _scHp = _scFoe.Hp;
            _scInput = new PlayerInput { Attack = true, Aim = new Vector2(1, 0.1f).Normalized() };
            _scFrame = 0;
            return;
        }
        if (_scFrame > 1) _scInput = default;
        ScShot($"drain_{_scFrame:00}");
        if (++_scFrame < 14) return;
        ScCheck($"the drain struck the golem (hp {_scHp:0} -> {_scFoe.Hp:0})", _scFoe.Hp < _scHp);
        ScEnd();
    }

    // ---------------------------------------------------------------- the cave mouth

    private int _scStep, _scRuns, _scEmbers;

    private void MouthScenario()
    {
        var p = G.Player;
        var cave = G.Cave;
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.4f) return;
                ScCheck($"depth 0 has a way out at the far left (at {cave.Mouth?.Round().ToString() ?? "none"})", cave.Mouth is Vector2 m && m.X < 100);
                if (cave.Mouth is not Vector2 mouth) { ScEnd(); return; }
                int i = (int)(mouth.X / CaveData.Cell), j = (int)(mouth.Y / CaveData.Cell) - 1;
                ScCheck("and you can walk there from the start", cave.ReachMask[j * cave.W + i]);
                // what the run has earned so far (it must all go when you leave)
                _scRuns = Meta.Runs - 1; // (the run just begun counted itself)
                _scEmbers = Meta.Embers;
                Meta.AddEmbers(7);
                ScShot("mouth_0");
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
            {
                // walk left to the daylight
                var mouth = cave.Mouth.Value;
                bool there = p.GlobalPosition.X < mouth.X + 18;
                _scInput = new PlayerInput { Move = new Vector2(there ? 0 : -1, 0) };
                if (_scT > 12f) { ScCheck("the hero reaches the cave mouth", false); ScEnd(); return; }
                if (!there || _scT < 1f) return;
                ScShot("mouth_1");
                // up alone never takes you out (it's a stray press so easily)
                _scInput = new PlayerInput { Up = true };
                _scStep = 2; _scT = 0;
                break;
            }
            case 2:
                // (each press is held a few frames, so a physics step always sees it)
                if (_scT > 0.2f) _scInput = default;
                if (_scT < 0.6f) return;
                ScCheck("pressing up at the mouth doesn't leave", _state == State.Playing);
                _scInput = new PlayerInput { Interact = true };
                _scStep = 3; _scT = 0;
                break;
            case 3:
                if (_scT > 0.2f) _scInput = default;
                if (_state == State.Playing && _scT < 3f) return;
                ScCheck("interact at the mouth ends the run, back to the title", _state == State.Title);
                ScCheck($"nothing from the run is kept (embers {_scEmbers} -> {Meta.Embers}, runs {_scRuns} -> {Meta.Runs})", Meta.Embers == _scEmbers && Meta.Runs == _scRuns);
                _scStep = 4; _scT = 0;
                break;
            case 4:
                if (_scT < 1f) return;
                ScShot("mouth_2");
                ScEnd();
                break;
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

    // ---------------------------------------------------------------- lava and Magma Skin

    private Chest _scChest;
    private float _scY;

    private void MagmaScenario()
    {
        var p = G.Player;
        var cave = G.Cave;
        float Burn() => Math.Min(p.Stats.MaxHp * 0.16f + 4, 30 * G.DepthDmg) * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult;
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.4f) return;
                int cells = 0;
                for (int j = 0; j < cave.H; j++) for (int i = 0; i < cave.W; i++) if (cave.IsLava(new Vector2((i + 0.5f) * CaveData.Cell, (j + 0.5f) * CaveData.Cell))) cells++;
                var sunk = _world.GetChildren().OfType<Chest>().Where(c => cave.IsLava(c.GlobalPosition + new Vector2(0, -10))).ToList();
                // (where the guardian's chamber lies lowest the lava can only be a thin film under
                // it: fewer chests fit then)
                bool lake = cells >= Tune.Drops.LavaLakeCells;
                ScCheck($"the lava ({cells} cells{(lake ? ", a lake" : ", a thin film")}) hides {(lake ? G.Biome.LavaCaches.ToString() : "some")} chests ({sunk.Count})",
                    lake ? sunk.Count == G.Biome.LavaCaches : cells == 0 ? sunk.Count == 0 : sunk.Count > 0 && sunk.Count <= G.Biome.LavaCaches);
                if (sunk.Count == 0) { GD.Print("[scenario] (no lava in this cave to swim in: try another --seed)"); ScEnd(); return; }
                _scChest = sunk[0];
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                p.Stats.MaxHp = 800; p.Hp = 800;
                // without the skin, lava burns and throws you out
                p.GlobalPosition = _scChest.GlobalPosition + new Vector2(0, -14);
                p.Velocity = Vector2.Zero;
                _scHp = p.Hp;
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
            {
                if (_scT < 0.15f) return;
                float took = _scHp - p.Hp;
                ScCheck($"lava burns a hero without Magma Skin ({took:0.0}, want {Burn():0.0}) and throws them up (vy {p.Velocity.Y:0})", Math.Abs(took - Burn()) < 0.5f && p.Velocity.Y < 0);
                ScShot("magma_0");
                _scStep = 2; _scT = 0;
                break;
            }
            case 2:
            {
                if (_scT < 1.2f) return;
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                Upgrades.Apply(Upgrades.Get("magma"), p.Stats, p);
                p.Hp = 800;
                p.GlobalPosition = _scChest.GlobalPosition + new Vector2(0, -14);
                p.Velocity = Vector2.Zero;
                _scHp = p.Hp;
                _scStep = 3; _scT = 0;
                break;
            }
            case 3:
            {
                foreach (var e in G.Enemies.ToArray()) e.QueueFree();
                if (_scT < 0.3f) return;
                if (_scY == 0) ScCheck($"with Magma Skin the hero swims in lava (in it {p.InWater})", p.InWater);
                _scY = 1;
                if (_scT < 2.4f) return;
                // (a tick every 0.7 s: three or four in the time)
                float took = _scHp - p.Hp, tick = Burn() * Tune.Hero.MagmaSkinBurn;
                ScCheck($"burned for {Tune.Hero.MagmaSkinBurn:P0} ({took:0.0} in 2.4 s, {tick:0.0} a tick), still in it ({p.InWater})", took > tick * 2.5f && took < tick * 4.5f && p.InWater);
                ScShot("magma_1");
                // open the chest down there
                p.GlobalPosition = _scChest.GlobalPosition + new Vector2(0, -8);
                _scInput = new PlayerInput { Interact = true };
                _scStep = 4; _scT = 0;
                break;
            }
            case 4:
                _scInput = default;
                // (the cards take a moment before they can be picked)
                if (_scT < 0.5f) return;
                ScCheck($"and opens a chest in the lava (cards up {_upgradeMenu.Visible})", _upgradeMenu.Visible);
                ScShot("magma_2");
                if (_upgradeMenu.Visible) _upgradeMenu.ChooseFirstOpen();
                _scY = p.GlobalPosition.Y;
                _scStep = 5; _scT = 0;
                break;
            case 5:
                _scInput = new PlayerInput { Move = new Vector2(0, -1), Up = true, JumpHeld = true };
                if (_scT < 0.8f) return;
                ScCheck($"and swims up to its surface ({_scY:0} -> {p.GlobalPosition.Y:0}, the surface at {cave.WaterY:0})", p.GlobalPosition.Y < cave.WaterY + 10);
                _scInput = default;
                ScEnd();
                break;
        }
    }

    // ---------------------------------------------------------------- the Elementals

    private readonly System.Collections.Generic.List<Enemy> _scElems = new();
    private readonly System.Collections.Generic.HashSet<Enemy> _scAttacked = new();
    private Enemy _scWater;
    private int _scPhase;

    /// <summary>Each walking Elemental is set on the hero on dry ground and must be seen winding up an attack; then the hero is put in the water beside a Water Elemental, which must stay in it and wind up too.</summary>
    private void ElementalsScenario()
    {
        var p = G.Player;
        if (_scPhase == 0)
        {
            if (_scT < 0.5f) return;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.Stats.MaxHp = 9000; p.Hp = 9000;
            int n = 0;
            for (int k = 0; k < 40 && n < 4; k++)
            {
                int side = k % 2 == 0 ? 1 : -1;
                var at = p.GlobalPosition + new Vector2(side * (100 + 26 * (k / 2)), -14);
                if (G.Cave.IsSolid(at) || G.Cave.IsWater(at) || !G.Cave.FindFloor(at, 60, out var fl) || G.Cave.IsWater(fl) || !G.Cave.LineClear(p.GlobalPosition, at)) continue;
                Enemy e = n switch { 0 => new EarthElemental(), 1 => new FrostElemental(), 2 => new NatureElemental(), _ => new FireElemental() };
                e.Position = fl - new Vector2(0, 16);
                e.SetMeta("test", true);
                _world.AddChild(e);
                e.Engage();
                _scElems.Add(e);
                n++;
            }
            GD.Print($"[scenario] {n} walkers set on the hero");
            _scPhase = 1; _scT = 0.6f;
            return;
        }
        if (_scPhase == 1)
        {
            foreach (var e in _scElems) if (IsInstanceValid(e) && e.Attacking) _scAttacked.Add(e);
            if (_scT < 14f) return;
            foreach (var e in _scElems) ScCheck($"{e.DisplayName} wound up an attack ({_scAttacked.Contains(e)})", _scAttacked.Contains(e));
            ScCheck($"all four found dry ground ({_scElems.Count})", _scElems.Count == 4);
            foreach (var e in _scElems) if (IsInstanceValid(e)) e.QueueFree();
            var wp = G.Cave.Spawns.Find(sp => sp.Kind == SpawnKind.Water);
            if (wp == null) { ScCheck("this cave has water", false); ScEnd(); return; }
            p.GlobalPosition = wp.Pos + new Vector2(70, 0);
            _scWater = new WaterElemental { Position = wp.Pos };
            _world.AddChild(_scWater);
            _scWater.Engage();
            _scPhase = 2; _scT = 0;
            return;
        }
        if (IsInstanceValid(_scWater) && _scWater.Attacking) _scAttacked.Add(_scWater);
        if (IsInstanceValid(_scWater) && !G.Cave.IsWater(_scWater.GlobalPosition) && _scT > 4f) { ScCheck("the Water Elemental stays in the water", false); ScEnd(); return; }
        if (_scT < 12f) return;
        ScCheck($"the Water Elemental is alive, in the water ({IsInstanceValid(_scWater) && G.Cave.IsWater(_scWater.GlobalPosition)}), and wound up an attack ({_scAttacked.Contains(_scWater)})",
            IsInstanceValid(_scWater) && G.Cave.IsWater(_scWater.GlobalPosition) && _scAttacked.Contains(_scWater));
        ScEnd();
    }

    /// <summary>--scenario=affinity: 100 damage of each kind on one of each kind of creature comes out as weakness and resistance say.</summary>
    private void AffinityScenario()
    {
        if (_scT < 0.5f) return;
        var p = G.Player;
        (Enemy foe, string what, (DamageKind k, float want)[] cases)[] rows =
        {
            (new EarthElemental(), "Earth Elemental", new[] { (DamageKind.Physical, 60f), (DamageKind.Water, 150f), (DamageKind.Nature, 150f), (DamageKind.Fire, 100f), (DamageKind.Frost, 100f) }),
            (new Golem(), "Golem (armoured)", new[] { (DamageKind.Physical, 60f), (DamageKind.Water, 100f), (DamageKind.Fire, 100f) }),
            (new FrostElemental(), "Frost Elemental", new[] { (DamageKind.Fire, 150f), (DamageKind.Frost, 60f), (DamageKind.Water, 60f), (DamageKind.Physical, 100f) }),
            (new FireElemental(), "Fire Elemental", new[] { (DamageKind.Water, 150f), (DamageKind.Fire, 60f), (DamageKind.Nature, 60f), (DamageKind.Frost, 100f) }),
            (new NatureElemental(), "Nature Elemental", new[] { (DamageKind.Fire, 150f), (DamageKind.Water, 60f), (DamageKind.Nature, 60f), (DamageKind.Physical, 100f) }),
            (new Goblin(), "Goblin", new[] { (DamageKind.Physical, 100f), (DamageKind.Fire, 100f), (DamageKind.Water, 100f) }),
        };
        foreach (var (foe, what, cases) in rows)
        {
            foe.Position = p.GlobalPosition + new Vector2(200, -10);
            foe.SetMeta("test", true);
            _world.AddChild(foe);
            foe.MaxHp = foe.Hp = 100000;
            foreach (var (k, want) in cases)
            {
                float dealt = foe.Hurt(100f, Vector2.Zero, foe.GlobalPosition, k);
                ScCheck($"{what} takes {want:0} of 100 {k} ({dealt:0})", Math.Abs(dealt - want) < 0.5f);
            }
        }
        ScEnd();
    }
}
