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
        "relics" => "den",
        "aegis" => "den",
        "shifter" => "den",
        "shifterforms" => "entrance",
        "loading" => "entrance",
        "abyss" => "slime",
        "nooks" => "slime",
        "lamp" => "ruins",
        "orbs" => "entrance",
        "ledges" => "slime",
        "ledgeab" => "roots",
        "rope" => "den",
        "coop" => "den",
        "shifterbehaviors" => "slime",
        "status" => "den",
        "guardians" => "den",
        "fall" => "den",
        "river" => "river",
        "stick" => "den",
        "glitch" => "den",
        "shifterspecials" => "entrance",
        "sfxvol" => "den",
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
            case "relics": RelicsScenario(); break;
            case "aegis": AegisScenario(); break;
            case "shifter": ShifterScenario(); break;
            case "shifterforms": ShifterFormsScenario(); break;
            case "loading": LoadingScenario(); break;
            case "abyss": AbyssScenario(); break;
            case "nooks": NooksScenario(); break;
            case "lamp": LampScenario(); break;
            case "orbs": OrbsScenario(); break;
            case "ledges": LedgesScenario(); break;
            case "ledgeab": LedgeAbScenario(); break;
            case "shifterbehaviors": ShifterBehaviorsScenario(); break;
            case "status": StatusScenario(); break;
            case "guardians": GuardiansScenario(); break;
            case "fall": FallScenario(); break;
            case "river": RiverScenario(); break;
            case "stick": StickScenario(); break;
            case "glitch": GlitchScenario(); break;
            case "shifterspecials": ShifterSpecialsScenario(); break;
            case "sfxvol": SfxVolScenario(); break;
            case "crab": CrabScenario(); break;
            case "rope": RopeScenario(); break;
            case "coop": CoopScenario(); break;
            case "rubble": RubbleScenario(); break;
            case "ice": IceScenario(); break;
            case "telegraph": TelegraphScenario(); break;
            case "share": ShareScenario(); break;
            case "crouch": CrouchScenario(); break;
            case "support": SupportScenario(); break;
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
        // armour falls off once it has turned aside a tenth of the creature's health: three blows of 100 (it turns 40 of each: 120 of 1000 > 100)
        var armoured = new Golem { Position = p.GlobalPosition + new Vector2(200, -10) };
        armoured.SetMeta("test", true);
        _world.AddChild(armoured);
        armoured.MaxHp = armoured.Hp = 1000;
        float a1 = armoured.Hurt(100f, Vector2.Zero, armoured.GlobalPosition), a2 = armoured.Hurt(100f, Vector2.Zero, armoured.GlobalPosition);
        bool held = !armoured.ArmorBroken;
        float a2b = armoured.Hurt(100f, Vector2.Zero, armoured.GlobalPosition);
        ScCheck($"an armoured golem takes 60 of 100 while its armour holds ({a1:0}, {a2:0}, {a2b:0}; still on after two {held}), and the armour is off once it has turned aside a tenth of its health ({armoured.ArmorBroken})",
            Math.Abs(a1 - 60f) < 0.5f && Math.Abs(a2 - 60f) < 0.5f && Math.Abs(a2b - 60f) < 0.5f && held && armoured.ArmorBroken);
        ScCheck($"and its blows now sound as on flesh ({armoured.HitSound})", armoured.HitSound == "hit");
        float a3 = armoured.Hurt(100f, Vector2.Zero, armoured.GlobalPosition);
        ScCheck($"and then it takes the whole of a blow ({a3:0})", Math.Abs(a3 - 100f) < 0.5f);
        ScEnd();
    }

    private Enemy _scCrab;
    private int _scDbg;
    private bool _scCrabWet, _scCrabPinched;

    /// <summary>--scenario=crab: a reef crab on a shore slab goes down into the water after a hero who waits there, snaps at them, and a level has shore slabs to find it on.</summary>
    private void CrabScenario()
    {
        var p = G.Player;
        if (_scPhase == 0)
        {
            if (_scT < 0.5f) return;
            var shores = G.Cave.Spawns.FindAll(sp => sp.Kind == SpawnKind.Shore);
            ScCheck($"this cave has shore slabs ({shores.Count})", shores.Count > 0);
            if (shores.Count == 0) { ScEnd(); return; }
            var sh = shores[0];
            int dir = Math.Sign(sh.Normal.X);
            // the hero waits in the water, down the slab
            var at = sh.Pos + new Vector2(dir * 140, 0);
            if (G.Cave.FindFloor(at + new Vector2(0, -20), 400, out var fl)) at = fl - new Vector2(0, 30);
            p.GlobalPosition = at;
            p.Stats.MaxHp = 5000; p.Hp = 5000;
            _scCrab = new Crab { Position = sh.Pos + new Vector2(0, -12) };
            _scCrab.SetMeta("test", true);
            _world.AddChild(_scCrab);
            _scCrab.Engage();
            GD.Print($"[scenario] crab at {sh.Pos}, hero at {p.GlobalPosition} (water at {G.Cave.WaterY})");
            _scPhase = 1; _scT = 0; _scAegisHp = p.Hp;
            return;
        }
        if (!IsInstanceValid(_scCrab)) { ScCheck("the crab lives", false); ScEnd(); return; }
        // (the crab on its own: nothing else near holds its attack slot)
        foreach (var e in G.Enemies.ToArray()) if (e != _scCrab && !e.IsQueuedForDeletion()) e.QueueFree();
        if (G.Cave.IsWater(_scCrab.GlobalPosition)) _scCrabWet = true;
        if (_scCrab.Attacking) _scCrabPinched = true;
        p.Hp = Math.Max(p.Hp, 4000);
        if ((int)(_scT * 2) != _scDbg) { _scDbg = (int)(_scT * 2); if (_scDbg % 4 == 0) GD.Print($"[scenario]   t {_scT:0.0}: crab {_scCrab.GlobalPosition.Round()} hero {p.GlobalPosition.Round()} wet {G.Cave.IsWater(_scCrab.GlobalPosition)} intent {_scCrab.IntentName} vel {_scCrab.Velocity.Round()} floor {_scCrab.IsOnFloor()} wall {_scCrab.IsOnWall()}"); }
        if (_scT < 16f) return;
        ScCheck($"the crab walked down into the water after the hero ({_scCrabWet}) and snapped its claws ({_scCrabPinched})", _scCrabWet && _scCrabPinched);
        ScCheck($"it still stands (at {_scCrab.GlobalPosition.Round()}, hp {_scCrab.Hp:0})", _scCrab.Hp > 0);
        ScEnd();
    }

    // ---------------------------------------------------------------- boulders in a passage
    private Rubble _scPlug;
    private bool _scShrineShot0;
    private int _scHeaves;

    private readonly System.Collections.Generic.List<Enemy> _scTele = new();
    private readonly System.Collections.Generic.HashSet<string> _scTeleSeen = new();

    /// <summary>A frog and a hunting spider face the hero: each must act out its telegraph (the throat swells, the spider crouches) before it strikes.</summary>
    private void TelegraphScenario()
    {
        var p = G.Player;
        if (_scPhase == 0)
        {
            if (_scT < 0.5f) return;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.Stats.MaxHp = 9000; p.Hp = 9000;
            for (int k = 0; k < 2; k++)
            {
                var at = p.GlobalPosition + new Vector2(60 + 30 * k, -14);
                if (!G.Cave.FindFloor(at, 80, out var fl)) fl = at;
                Enemy e = k == 0 ? new Frog() : new Spider { Grounded = true };
                e.Position = fl - new Vector2(0, 14);
                e.SetMeta("test", true);
                _world.AddChild(e);
                e.Engage();
                _scTele.Add(e);
            }
            _scPhase = 1; _scT = 0;
            return;
        }
        p.Hp = 9000;
        foreach (var e in _scTele)
            if (IsInstanceValid(e) && e.Animator?.Sprite != null) _scTeleSeen.Add(e.Animator.Sprite.Animation.ToString());
        if (_scT < 10f) return;
        bool Saw(string n) => _scTeleSeen.Contains(n + "_r") || _scTeleSeen.Contains(n + "_l");
        ScCheck($"the frog swells its throat before the tongue ({Saw("tongue_windup")}, then {Saw("tongue")})", Saw("tongue_windup") && Saw("tongue"));
        ScCheck($"the spider crouches before it pounces ({Saw("pounce_windup")}, then {Saw("pounce")})", Saw("pounce_windup") && Saw("pounce"));
        ScEnd();
    }

    private void IceScenario()
    {
        if (_scT < 0.5f) return;
        var at = G.Player.GlobalPosition + new Vector2(60, -20);
        var a = new IceSheet { Position = at, HalfW = 24 }; _world.AddChild(a);
        var b = new IceSheet { Position = at + new Vector2(0, 80), HalfW = 24 }; _world.AddChild(b);
        for (int i = 1; i <= 3; i++)
        {
            Breakables.Spell(at + new Vector2(-40, 0), at + new Vector2(40, 0));
            System.Threading.Thread.Sleep(150);
            ScCheck($"touch {i}: the ice {(i < 3 ? "holds" : "breaks")}", i < 3 ? GodotObject.IsInstanceValid(a) && !a.IsQueuedForDeletion() : a.IsQueuedForDeletion());
        }
        ScCheck("a spell that misses it does nothing", GodotObject.IsInstanceValid(b) && !b.Cracked);
        ScEnd();
    }

    private void RubbleScenario()
    {
        var p = G.Player;
        if (_scPhase == 0)
        {
            if (_scT < 0.5f) return;
            // (a cave with no passage narrow enough for a plug gets one set down beside the hero, to look at)
            // (and with --shots, a fresh one on the flat in front of the hero, so the pictures are of a plug on level ground)
            Rubble test = null;
            if ((Rubble.All.Count == 0 || _shotDir != "") && G.Cave.FindFloor(p.GlobalPosition + new Vector2(120, -40), 200, out var pf))
                _world.AddChild(test = new Rubble { Position = pf + new Vector2(0, -32), Size = new Vector2(32, 64), Index = 1 });
            ScCheck($"this cave has boulder plugs ({Rubble.All.Count})", Rubble.All.Count > 0);
            if (Rubble.All.Count == 0) { ScEnd(); return; }
            _scPlug = test ?? Rubble.All[0];
            // the hero stands at the plug's foot, on its left
            var at = _scPlug.GlobalPosition + new Vector2(-_scPlug.Size.X * 0.5f - 14, _scPlug.Size.Y * 0.5f - 12);
            p.GlobalPosition = at; p.Velocity = Vector2.Zero;
            ScCheck("the plug is solid", _scPlug.CollisionLayer != 0 && !_scPlug.GetChild<CollisionShape2D>(0).Disabled);
            _scPhase = 1; _scT = 0;
            return;
        }
        if (_scPhase == 4)
        {
            // (with --shots=DIR: the nearest shrine, as it starts and after it has sunk to the floor)
            Chest shrine = null;
            foreach (var c in Chest.All) if (GodotObject.IsInstanceValid(c) && c.IsShrine) { shrine = c; break; }
            if (shrine == null) { ScEnd(); return; }
            p.GlobalPosition = shrine.GlobalPosition + new Vector2(-50, -10);
            if (_scT > 0.4f && !_scShrineShot0) { _scShrineShot0 = true; GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/shrine_0.png"); }
            if (_scT > 8f) { GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/shrine_1.png"); ScEnd(); }
            return;
        }
        p.Hp = Math.Max(p.Hp, 1000);
        p.GlobalPosition = new Vector2(_scPlug.GlobalPosition.X - _scPlug.Size.X * 0.5f - 14, p.GlobalPosition.Y);
        if (_scPhase == 1)
        {
            ScCheck("the hero is within reach of it", _scPlug.Reaches(p.GlobalPosition));
            if (_shotDir != "") GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/rubble_0.png");
            _scPhase = 2; _scT = 0;
            return;
        }
        if (_scPhase == 2)
        {
            if (_scT > 0.25f) { _scT = 0; if (!_scPlug.Cleared) { _scPlug.Heave(); _scHeaves++; if (_shotDir != "" && _scHeaves == 2) GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/rubble_1.png"); } }
            if (_scPlug.Cleared)
            {
                ScCheck($"{_scHeaves} heaves clear it (it takes {Tune.Rubble.Hits})", _scHeaves == Tune.Rubble.Hits);
                _scPhase = 3; _scT = 0;
            }
            return;
        }
        if (_scT > 0.3f)
        {
            ScCheck("the way is open: no collision left", _scPlug.GetChild<CollisionShape2D>(0).Disabled);
            if (_shotDir != "") { GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/rubble_2.png"); _scPhase = 4; _scT = 0; return; }
            ScEnd();
        }
    }

    // ---------------------------------------------------------------- blows are shared by those near
    private void ShareScenario()
    {
        var p = G.Player;
        if (_scT < 0.5f) return;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        var friend = new Player { Stats = new PlayerStats(HeroKind.Swordsman), Position = p.GlobalPosition + new Vector2(40, 0) };
        friend.InputOverride = () => default;
        friend.Stats.HurtInvuln = 0f;
        _world.AddChild(friend);
        var foe = new Golem { Position = p.GlobalPosition + new Vector2(-60, -10) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        foe.Freeze(99f, hold: true);
        p.Stats.MaxHp = 500; p.Hp = 500; friend.Stats.MaxHp = 500; friend.Hp = 500;
        p.Stats.HurtInvuln = 0f;
        float a0 = p.Hp, b0 = friend.Hp;
        p.Hurt(20f, foe.GlobalPosition, 0f, foe);
        float a = a0 - p.Hp, b = b0 - friend.Hp;
        ScCheck($"a blow of 20 near a friend: each takes half ({a:0.0}, {b:0.0})", Math.Abs(a - 10f) < 1.5f && Math.Abs(b - 10f) < 1.5f);
        // far apart: the whole of it
        friend.GlobalPosition = p.GlobalPosition + new Vector2(400, 0);
        p.Hp = 500; friend.Hp = 500;
        p.Hurt(20f, foe.GlobalPosition, 0f, foe);
        ScCheck($"a friend far off takes none, the one struck all ({500 - p.Hp:0.0}, {500 - friend.Hp:0.0})", Math.Abs(500 - p.Hp - 20f) < 1.5f && friend.Hp >= 499.9f);
        // bubbles: far apart, one pays it all; side by side they split it
        foreach (var d in new[] { 200f, 4f })
        {
            friend.GlobalPosition = p.GlobalPosition + new Vector2(d, 0);
            p.TestClearBubble(); friend.TestClearBubble();
            p.GiveBubble(100f, 30f, false); friend.GiveBubble(100f, 30f, false);
            p.Hurt(20f, foe.GlobalPosition, 0f, null);
            float mine = 100f - p.BubbleHp, theirs = 100f - friend.BubbleHp;
            ScCheck($"bubbles {d:0} px apart soak 10 of a blow of 20: this one pays {mine:0.0}, the friend's {theirs:0.0}", d > 100f ? Math.Abs(mine - 10f) < 0.6f && theirs < 0.1f : Math.Abs(mine - 5f) < 1f && Math.Abs(theirs - 5f) < 1f);
        }
        ScEnd();
    }

    // ---------------------------------------------------------------- ducking
    private void CrouchScenario()
    {
        var p = G.Player;
        if (_scPhase == 0)
        {
            if (_scT < 0.5f) return;
            foreach (var e in G.Enemies.ToArray()) e.QueueFree();
            p.Stats.MaxHp = 500; p.Hp = 500; p.Stats.HurtInvuln = 0f;
            _scInput = new PlayerInput { Move = new Vector2(0, 1) };
            _scPhase = 1; _scT = 0;
            return;
        }
        if (_scT < 0.6f) return;
        ScCheck($"holding down on the ground crouches ({p.Crouching})", p.Crouching);
        if (_shotDir != "") GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/crouch.png");
        var foe = new Golem { Position = p.GlobalPosition + new Vector2(-60, -10) };
        foe.SetMeta("test", true);
        _world.AddChild(foe); foe.Freeze(99f, hold: true);
        float hp0 = p.Hp;
        p.Hurt(20f, p.GlobalPosition + new Vector2(-20, -30), 0f, foe);
        ScCheck($"a blow from above the head misses a crouching hero ({hp0 - p.Hp:0.0} lost)", p.Hp >= hp0 - 0.01f);
        hp0 = p.Hp;
        p.Hurt(20f, p.GlobalPosition + new Vector2(-20, 4), 0f, foe);
        ScCheck($"a blow at the body still lands ({hp0 - p.Hp:0.0} lost)", hp0 - p.Hp > 5f);
        _scInput = default;
        ScEnd();
    }

    // ---------------------------------------------------------------- every hero's support ability
    private void SupportScenario()
    {
        var p = G.Player;
        if (_scT < 0.5f) return;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = 500; p.Hp = 500;
        var friend = new Player { Stats = new PlayerStats(HeroKind.Swordsman), Position = p.GlobalPosition + new Vector2(60, 0) };
        friend.InputOverride = () => default;
        _world.AddChild(friend);
        Enemy Foe(float dx)
        {
            var g = new Golem { Position = p.GlobalPosition + new Vector2(dx, -10) };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.MaxHp = g.Hp = 900; g.Freeze(99f, hold: true);
            return g;
        }
        var aim = new Vector2(1, 0);

        p.TestHero = HeroKind.Swordsman;
        float dm = friend.Stats.DamageMult, dmMe = p.Stats.DamageMult;
        bool ok = p.TestSupport(aim);
        ScCheck($"Battle Shout: allies near (and you) hit {Tune.Support.ShoutDamage - 1:0%} harder ({friend.Stats.DamageMult / dm:0.00}, {p.Stats.DamageMult / dmMe:0.00})",
            ok && Math.Abs(friend.Stats.DamageMult / dm - Tune.Support.ShoutDamage) < 0.001f && Math.Abs(p.Stats.DamageMult / dmMe - Tune.Support.ShoutDamage) < 0.001f);
        friend.TestExpire(); p.TestExpire();
        ScCheck($"...and it wears off ({friend.Stats.DamageMult / dm:0.000})", Math.Abs(friend.Stats.DamageMult / dm - 1f) < 0.001f && !friend.HasBuff(Player.BuffShout));

        p.TestHero = HeroKind.ShapeShifter;
        float mv = friend.Stats.MoveSpeed;
        ok = p.TestSupport(aim);
        ScCheck($"Pack Howl: allies near run faster ({friend.Stats.MoveSpeed / mv:0.00})", ok && Math.Abs(friend.Stats.MoveSpeed / mv - Tune.Support.HowlMove) < 0.001f);
        friend.TestExpire(); p.TestExpire();
        ScCheck($"...and it wears off ({friend.Stats.MoveSpeed / mv:0.000})", Math.Abs(friend.Stats.MoveSpeed / mv - 1f) < 0.001f);

        p.TestHero = HeroKind.Warden;
        ok = p.TestSupport(aim);
        ScCheck($"Taunt: she counts as next to nothing away ({p.EffectiveThreat:0.000})", ok && p.Taunting && p.EffectiveThreat < 0.01f);
        p.TestExpire();
        ScCheck($"...and it ends ({p.EffectiveThreat:0.00})", !p.Taunting && p.EffectiveThreat > 0.5f);

        p.TestHero = HeroKind.Vitalist;
        p.SetVitalForce(0); p.Hp = 500; float vf0 = p.VitalForce;
        ok = p.TestSupport(aim);
        float cost = 500 - p.Hp, gain = p.VitalForce - vf0;
        ScCheck($"Health Tap: {cost:0} health for {gain:0.0} vital force", ok && Math.Abs(cost - 500 * Tune.Support.TapCost) < 0.5f && gain > 0);
        p.Hp = 500;

        p.TestHero = HeroKind.Elementalist;
        var f1 = Foe(120); float h1 = f1.Hp;
        ok = p.TestSupport(aim);
        ScCheck($"Stalag-Might: {h1 - f1.Hp:0.0} damage and the creature is held ({f1.Reeling})", ok && Math.Abs(h1 - f1.Hp - Tune.Support.StalagDamage * p.Stats.DamageMult * Affinity.Mult(f1.Element, DamageKind.Physical)) < 3f && f1.Reeling);

        ScCheck($"...a bulge of rock holds it ({System.Linq.Enumerable.Count(_world.GetChildren(), n => n is StalagGrip)})", System.Linq.Enumerable.Any(_world.GetChildren(), n => n is StalagGrip));
        f1.GlobalPosition += new Vector2(0, -600);
        var hi = Foe(100); hi.GlobalPosition += new Vector2(0, -150);
        ok = p.TestSupport(aim);
        ScCheck($"Stalag-Might can't seize a creature that isn't near the ground ({ok})", !ok && !hi.Reeling);
        hi.QueueFree();

        p.TestHero = HeroKind.Rogue;
        var f2 = Foe(-120); f2.FaceToward(1);
        p.Facing = -1;
        float h2 = f2.Hp;
        ok = p.TestSupport(new Vector2(-1, 0));
        f2.Hurt(100f, Vector2.Zero, f2.GlobalPosition, DamageKind.Raw);
        ScCheck($"Expose: a blow of 100 does {h2 - f2.Hp:0.0} to the marked creature", ok && (Math.Abs(h2 - f2.Hp - 100f * Tune.Support.ExposeVuln) < 2f || Math.Abs(h2 - f2.Hp - 200f * Tune.Support.ExposeVuln) < 2f)); // (a crit doubles it: Expose makes them likelier)

        p.TestHero = HeroKind.Aegis;
        p.Facing = 1;
        f1.QueueFree();
        var f3 = Foe(60);
        ok = p.TestSupport(aim);
        p.Hp = 480;
        f3.Hurt(10f, Vector2.Zero, f3.GlobalPosition, DamageKind.Raw);
        ScCheck($"Mending Mark: marked ({ok}), and the one who strikes it is healed ({p.Hp - 480:0.0})", ok && Math.Abs(p.Hp - 480 - Tune.Support.MarkHeal * p.Stats.WardMult) < 0.6f);
        f3.Hurt(10f, Vector2.Zero, f3.GlobalPosition, DamageKind.Raw);
        ScCheck($"...once ({p.Hp - 480:0.0})", Math.Abs(p.Hp - 480 - Tune.Support.MarkHeal * p.Stats.WardMult) < 0.6f);
        ScEnd();
    }
}
