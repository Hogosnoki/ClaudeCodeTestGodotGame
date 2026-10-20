using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Warden (earth, frost, wind; <see cref="Tune.ElementalWarden"/>): his three buttons each take an element, and two charges
/// readied make the pair's own version of any of them (every pair has one on each button, <see cref="Elements.DualName"/>).
/// The attack (the shortsword): Stonecutter, Rime Edge, Gale Edge; Boulder Strike, Icicle Lance, Cyclone Slash, Frozen Earth, Sandblast,
/// Frost Gust. The block (the shield raised with charges readied; the guard lasts as long as it stays up): Stone Guard, Rime Guard, Gale
/// Guard; Bastion, Glacial Bulwark, Eye of the Storm, Permafrost Guard, Dust Wall, Hoarfrost Gale. The dash (the Guarded Charge, which in
/// every version runs through creatures instead of stopping at one): Rockslide Charge, Frozen Tundra, Gale Charge; Avalanche, Glacier
/// Charge, Tempest Charge, Frostquake, Duststorm, Blizzard Charge. His Shield Bash isn't in the elemental kit; Taunt is as it was.
/// </summary>
public partial class Player
{
    /// <summary>The version on the swing under way (for its blows), and its elements.</summary>
    private string _wSwing = "";
    private HeroElement _wA, _wB;
    /// <summary>The guard on the shield (while it stays up), its elements and how long it can last.</summary>
    private string _guard = "";
    private HeroElement _guardA, _guardB;
    private float _guardT, _guardTick;
    /// <summary>The version of the charge under way, its elements, its speed, where it began; what it carries along (the Tempest).</summary>
    private string _wDash = "";
    private HeroElement _dashA, _dashB;
    private float _dashSpeedMult = 1f, _dashCarryT, _dashDustAt;
    private Vector2 _dashFrom;
    private readonly List<Enemy> _carried = new();
    /// <summary>Clouds of dust left by a Dust Wall or a Duststorm: where, and how long each has left (creatures in them are sapped).</summary>
    private readonly List<(Vector2 at, float t)> _dusts = new();
    private float _dustTick;

    /// <summary>For the HUD and the tests: the guard on the shield now ("" none) and the version of the charge under way.</summary>
    public string GuardName => _guard;
    public float GuardLeft => _guard != "" ? _guardT : 0f;
    public HeroElement GuardElement => _guard != "" ? _guardA : HeroElement.None;
    public string DashVersion => _wDash;

    private float WardBlow => BaseDamage * Stats.DamageMult;

    /// <summary>What a Warden button spends: the pair (every pair has a version on each of his buttons), or the oldest charge.</summary>
    private (string name, HeroElement a, HeroElement b) SpendFor(ElemSlot slot)
    {
        if (DualReady)
        {
            string n = DualNameFor(slot);
            var (a, b) = SpendPair();
            return (n, a, b);
        }
        var e = SpendElement();
        return (Elements.SingleName(Stats.Hero, slot, e), e, HeroElement.None);
    }

    private static bool PairHas(HeroElement a, HeroElement b, HeroElement x) => a == x || b == x;

    private void WardUsed(string name, HeroElement a, HeroElement b)
    {
        _lastEl = a;
        ElementalUses.Add(name);
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), name.ToUpperInvariant(), col, b == HeroElement.None ? 11 : 12, 0.8f);
    }

    /// <summary>The ground under a point (or the point itself, low as the feet, where there's none near).</summary>
    private Vector2 FloorAt(Vector2 p) => Ground.Below(p, 60, out var floor) ? floor : p + new Vector2(0, 13);

    /// <summary>Every creature in a cone in front: within <paramref name="range"/>, <paramref name="halfDeg"/> degrees either side of <paramref name="dir"/>.</summary>
    private IEnumerable<Enemy> InCone(Vector2 origin, Vector2 dir, float range, float halfDeg)
    {
        float half = Mathf.DegToRad(halfDeg);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - origin;
            if (to.Length() - e.HitRadius > range) continue;
            if (to.Length() > 12 && Math.Abs(dir.AngleTo(to)) > half + MathF.Atan2(e.HitRadius, Math.Max(1f, to.Length()))) continue;
            if (!G.Cave.LineClear(origin, e.GlobalPosition)) continue;
            yield return e;
        }
    }

    private void TickWardenElements(float dt)
    {
        TickModeGuards(dt);
        if (_guard != "")
        {
            if (!ShieldRaised || ShieldBroken || (_guardT -= dt) <= 0) EndGuard();
            else TickGuard(dt);
        }
        if (_dusts.Count > 0)
        {
            for (int k = _dusts.Count - 1; k >= 0; k--)
            {
                var d = _dusts[k];
                d.t -= dt;
                if (d.t <= 0) _dusts.RemoveAt(k); else _dusts[k] = d;
            }
            if ((_dustTick -= dt) <= 0)
            {
                _dustTick = 0.5f;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit) continue;
                    foreach (var d in _dusts)
                        if (e.GlobalPosition.DistanceTo(d.at) < Tune.ElementalWarden.DustRadius + e.HitRadius) { e.Sap(1.2f); break; }
                }
            }
        }
    }

    // ================================================================== the attack

    /// <summary>The attack button: the shortsword, or with charges readied the element's (or the pair's) cut.</summary>
    private bool WardenSwing(Vector2 aim, bool held)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        if (Infused == HeroElement.None) return TrySwing(aim, held);
        var (name, a, b) = SpendFor(ElemSlot.Attack);
        if (_dodgeT > 0) _dodgeT = 0;
        // (every swing goes at its mode's pace: earth slow, frost steady, wind quick)
        StartSwing(aim, pace: WardenPace(Mode));
        _wSwing = name; _wA = a; _wB = b; _wMode = Mode;
        var dir = _swingDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        switch (name)
        {
            case "Stonecutter":
                // slow and heavy: the sword comes round like a slab of rock
                _swingDmg *= Tune.ElementalWarden.StoneDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                G.Fx.Debris(GlobalPosition + new Vector2(0, 10), Elements.ColorOf(HeroElement.Earth), 3, 60);
                break;
            case "Rime Edge":
                _swingDmg *= Tune.ElementalWarden.RimeDamage; _swingEl = HeroElement.Frost; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                G.Fx.Burst(origin + dir * 10, new Color(0.9f, 0.97f, 1f), 4, 40, 1.6f, 0.3f, -20);
                break;
            case "Gale Edge":
                // quick and light, flinging what it strikes
                _swingDmg *= Tune.ElementalWarden.GaleDamage; _swingReach *= Tune.ElementalWarden.GaleReach; _swingEl = HeroElement.Wind;
                break;
            case "Boulder Strike":
                // a heavy blow of stone: stunned, the frozen shattered, tossed
                _swingDmg *= Tune.ElementalWarden.BoulderDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.Elements.Full; _swingShatter = true;
                G.Fx.Debris(origin + dir * 16, Elements.ColorOf(HeroElement.Earth), 8, 180);
                G.Fx.AddShake(2.5f);
                break;
            case "Icicle Lance":
                // the cut, and a lance of ice thrown on along the aim
                _swingEl = HeroElement.Frost; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                var lance = new ElementBolt
                {
                    Position = origin + dir * 10f, Dir = dir, Range = Tune.ElementalWarden.LanceRange, Caster = this, Frost = true,
                    BuildMult = Tune.Elements.Full / Math.Max(1f, Tune.ElementalStaff.FrostBuild), Damage = WardBlow * Tune.ElementalWarden.LanceDamage,
                };
                G.Spawn(lance);
                NetSync.HeroVisual(lance);
                G.Sfx.Play("clink", origin, -4, 0.05f, 1.5f);
                break;
            case "Cyclone Slash":
                // a whirling cut all round
                _swingArc = Mathf.Tau; _swingReach *= Tune.ElementalWarden.CycloneReach; _swingDmg *= Tune.ElementalWarden.CycloneDamage; _swingEl = HeroElement.Wind;
                G.Fx.Ring(origin, _swingReach * 0.8f, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 0.3f, FxLayer.RingStyle.Burst);
                for (int k = 0; k < 10; k++) G.Fx.Directional(origin + Vector2.Right.Rotated(k * Mathf.Tau / 10f) * 12f, Vector2.Right.Rotated(k * Mathf.Tau / 10f + 1.4f), 0.2f, new Color(0.85f, 1f, 0.9f, 0.7f), 1, 220, 1.6f, 0.25f, 0, 1);
                G.Sfx.Play("dodge", origin, -2, 0.05f, 0.8f);
                break;
            case "Frozen Earth":
                _swingDmg *= Tune.ElementalWarden.FrozenDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.ElementalWarden.FrozenBuild; _swingShatter = true;
                G.Fx.Burst(origin + dir * 14, new Color(0.9f, 0.97f, 1f), 6, 80, 1.8f, 0.3f, 40);
                G.Fx.Debris(origin + dir * 14, Elements.ColorOf(HeroElement.Earth), 4, 120);
                break;
            case "Sandblast":
                // a cone of grit thrown off the cut: nicked and sapped
                _swingEl = HeroElement.Earth;
                foreach (var e in InCone(origin, dir, Tune.ElementalWarden.ConeRange, Tune.ElementalWarden.ConeHalf))
                {
                    float d = e.Hurt(WardBlow * Tune.ElementalWarden.SandDamage, dir * 40f, e.GlobalPosition, DamageKind.Physical);
                    if (d > 0) OnDealtDamage(d);
                    if (!e.Dead) e.Sap(Tune.ElementalWarden.SandSap);
                }
                G.Fx.Directional(origin, dir, Mathf.DegToRad(Tune.ElementalWarden.ConeHalf), new Color(0.72f, 0.6f, 0.42f, 0.85f), 22, 300, 2.4f, 0.4f, 60, 0);
                G.Fx.Dust(origin + dir * 30, 6, 2f, new Color(0.6f, 0.5f, 0.38f, 0.6f));
                G.Sfx.Play("rock", origin, -6, 0.05f, 1.3f);
                break;
            case "Frost Gust":
                // a freezing wind off the cut: filled with frost and driven back
                _swingEl = HeroElement.Frost;
                foreach (var e in InCone(origin, dir, Tune.ElementalWarden.ConeRange, Tune.ElementalWarden.ConeHalf))
                {
                    e.Build(HeroElement.Frost, Tune.ElementalWarden.GustBuild, Stats.DamageMult, this);
                    e.Shove(dir * Tune.ElementalWarden.GustShove + new Vector2(0, -50f));
                }
                G.Fx.Directional(origin, dir, Mathf.DegToRad(Tune.ElementalWarden.ConeHalf), new Color(0.88f, 0.97f, 1f, 0.85f), 22, 320, 2.2f, 0.4f, 0, 1);
                G.Sfx.Play("dodge", origin, -4, 0.05f, 0.7f);
                break;
        }
        WardUsed(name, a, b);
        return true;
    }

    /// <summary>The mode the swing under way was made in (its look as the blade comes round).</summary>
    private HeroElement _wMode;

    /// <summary>A mode's swing pace (x the wind-up, the swing and its cooldown).</summary>
    private static float WardenPace(HeroElement mode) => mode switch
    {
        HeroElement.Earth => Tune.ElementalWarden.StonePace, HeroElement.Frost => Tune.ElementalWarden.RimePace,
        HeroElement.Wind => Tune.ElementalWarden.GalePace, _ => 1f,
    };

    /// <summary>The blade comes round: how it looks and sounds by its mode (a slab of rock, a sheet of rime, a gust).</summary>
    private void WardenSwingRelease()
    {
        var dir = _swingDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        var tip = origin + dir * _swingReach * 0.8f;
        var col = Elements.ColorOf(_wMode);
        switch (_wMode)
        {
            case HeroElement.Earth:
                // the weight of it: the ground jolts, grit flies off the blade, a low crack
                G.Fx.AddShake(2f);
                G.Fx.Debris(tip, col, 6, 170);
                G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 5, 1.6f, new Color(0.6f, 0.5f, 0.38f, 0.6f));
                for (int k = 0; k < 3; k++) G.Fx.Burst(origin + dir.Rotated((k - 1) * 0.5f) * _swingReach * 0.6f, new Color(col, 0.8f), 2, 50, 2.4f, 0.3f, 200);
                G.Sfx.Play("rock", tip, -3, 0.08f, 0.7f);
                Anim.Punch(new Vector2(1.3f, 0.8f));
                break;
            case HeroElement.Frost:
                // a sheet of rime along the arc, glittering, and a cold ring
                for (int k = 0; k < 5; k++)
                {
                    var at = origin + dir.Rotated((k - 2) * 0.35f) * _swingReach * 0.75f;
                    G.Fx.Burst(at, new Color(0.88f, 0.97f, 1f, 0.9f), 2, 30, 1.5f, 0.45f, -10);
                }
                G.Fx.Directional(origin + dir * 8f, dir, 0.6f, new Color(col, 0.6f), 6, 120, 1.6f, 0.4f, 0, 0);
                G.Sfx.Play("clink", tip, -8, 0.1f, 1.4f);
                break;
            case HeroElement.Wind:
                // a gust ahead of the blade, streaks and an afterimage
                G.Fx.Directional(origin + dir * 6f, dir, 0.35f, new Color(col, 0.8f), 10, 340, 1.6f, 0.22f, 0, 1);
                Afterimage.Spawn(Anim, new Color(col, 0.7f), 0.15f);
                G.Sfx.Play("dodge", tip, -6, 0.1f, 1.5f);
                break;
        }
    }

    /// <summary>A blow of the Warden's elemental cut lands: what the version adds beyond its build-up.</summary>
    private void WardenBladeHit(Enemy e)
    {
        if (_wSwing == "" || e.Dead) return;
        var away = (e.GlobalPosition - GlobalPosition).Normalized();
        switch (_wSwing)
        {
            case "Gale Edge": e.Shove(away * Tune.ElementalWarden.GaleShove + new Vector2(0, -90f)); break;
            case "Stonecutter": G.Fx.Debris(e.GlobalPosition, Elements.ColorOf(HeroElement.Earth), 5, 150); e.HitStop(0.06f); break;
            case "Cyclone Slash": e.Shove(away * Tune.ElementalWarden.CycloneShove + new Vector2(0, -80f)); break;
            case "Boulder Strike": e.Shove(new Vector2(away.X * 140f, -Tune.ElementalWarden.BoulderToss)); break;
            case "Frozen Earth": e.Build(HeroElement.Frost, Tune.ElementalWarden.FrozenBuild, Stats.DamageMult, this); break;
        }
    }

    // ================================================================== the block

    /// <summary>The shield goes up in a mode: with nothing on top, the mode's own guard (Earth Shield, Frost Shield, Wind Barrier); with a
    /// pair on top, the pair's guard, which holds while the shield stays up.</summary>
    private void GuardRaised()
    {
        if (!Elemental || Infused == HeroElement.None) return;
        if (!DualReady) { RaiseModeGuard(); return; }
        var (name, a, b) = SpendFor(ElemSlot.Heavy);
        _guard = name; _guardA = a; _guardB = b;
        _guardT = Tune.ElementalWarden.GuardSeconds;
        _guardTick = 0f;
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        var front = GlobalPosition + new Vector2(0, -3) + ShieldDir * 16f;
        G.Fx.Ring(front, 16, new Color(col, 0.95f), 0.3f, FxLayer.RingStyle.Burst);
        switch (name)
        {
            case "Glacial Bulwark":
            {
                // a wall of ice rises in front (creatures go for it; friends walk through it)
                float side = Math.Abs(ShieldDir.X) > 0.2f ? Math.Sign(ShieldDir.X) : Facing;
                var at = FloorAt(GlobalPosition + new Vector2(side * 26f, 0));
                var wall = new FrostTower
                {
                    Position = at, Caster = this, Height = Tune.ElementalWarden.WallHeight, Half = Tune.ElementalWarden.WallHalf, Seconds = Tune.ElementalWarden.WallSeconds,
                    MaxHp = Tune.ElementalWarden.WallHp * Stats.MaxHp / Math.Max(1f, Tune.Warden.StartHp),
                };
                G.Spawn(wall);
                LastTower = wall;
                break;
            }
            case "Permafrost Guard":
                Launch(new TundraField { Position = FloorAt(GlobalPosition), Half = Tune.ElementalWarden.PermafrostHalf, Seconds = Tune.ElementalWarden.GuardSeconds });
                break;
            case "Dust Wall":
            {
                var at = GlobalPosition + ShieldDir * 30f;
                var cloud = new SmokeCloud { Position = at, Radius = Tune.ElementalWarden.DustRadius, Life = Tune.ElementalWarden.DustSeconds };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                _dusts.Add((at, Tune.ElementalWarden.DustSeconds));
                G.Fx.Dust(at, 10, 2.4f, new Color(0.62f, 0.52f, 0.4f, 0.7f));
                break;
            }
            case "Eye of the Storm":
                G.Fx.Ring(GlobalPosition, Tune.ElementalWarden.StormRadius, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 0.4f, FxLayer.RingStyle.Dashed);
                break;
        }
        G.Sfx.Play(a == HeroElement.Frost ? "clink" : a == HeroElement.Wind ? "dodge" : "rock", GlobalPosition, -6, 0.05f, 0.8f);
        WardUsed(name, a, b);
    }

    private void EndGuard() { _guard = ""; _guardA = _guardB = HeroElement.None; }

    // ================================================================== the mode's own guards

    /// <summary>The Earth Shield's time left and its rest; the shell on him (frost or wind), what it has soaked, its rest, its flash.</summary>
    private float _moundT, _moundRest, _shellRest, _shellSoaked, _shellFlash;
    private HeroElement _shell;
    private EarthMound _mound;
    /// <summary>A puppet's guards, as its game last sent them.</summary>
    private bool _netMound;
    private HeroElement _netShell;

    /// <summary>The Earth Shield stands: nothing reaches him, and nothing moves him (nor does he move).</summary>
    public bool EarthShieldUp => IsRemote ? _netMound : _moundT > 0;
    public float EarthShieldLeft => Math.Max(0f, _moundT);
    /// <summary>The shell on him (Frost Shield or Wind Barrier; none), how much more it can soak (0..1), and its flash when struck.</summary>
    public HeroElement ShellElement => IsRemote ? _netShell : _shell;
    public float ShellLeft => IsRemote ? 1f : _shell == HeroElement.None ? 0f : Math.Clamp(1f - _shellSoaked / ShellHold(_shell), 0f, 1f);
    public float ShellSoaked => _shellSoaked;
    public float ShellFlash => Math.Max(0f, _shellFlash);
    /// <summary>For the HUD: the mode's own guard's rest (seconds left, and 0..1 of it).</summary>
    public float ModeGuardRest => Math.Max(0f, Mode == HeroElement.Earth ? _moundRest : _shellRest);
    public float ModeGuardRestFrac => ModeGuardRest / Math.Max(0.01f, Mode switch
    {
        HeroElement.Earth => Tune.ElementalWarden.MoundRest, HeroElement.Frost => Tune.ElementalWarden.FrostShellRest, _ => Tune.ElementalWarden.WindShellRest,
    });
    private static float ShellHold(HeroElement el) => el == HeroElement.Frost ? Tune.ElementalWarden.FrostShellHold : Tune.ElementalWarden.WindShellHold;
    private static float ShellShare(HeroElement el) => el == HeroElement.Frost ? Tune.ElementalWarden.FrostShellShare : Tune.ElementalWarden.WindShellShare;

    /// <summary>The shield raised in a mode with nothing on top: earth heaves up before him, or a shell of ice or wind closes about him
    /// (neither while the last still holds or rests; the shield is only the shield meanwhile).</summary>
    private void RaiseModeGuard()
    {
        var el = Mode;
        switch (el)
        {
            case HeroElement.Earth:
                if (_moundT > 0 || _moundRest > 0 || InWater || !IsOnFloor()) return;
                StartEarthShield();
                break;
            case HeroElement.Frost:
            case HeroElement.Wind:
                if (_shell != HeroElement.None || _shellRest > 0) return;
                RaiseShell(el);
                break;
            default: return;
        }
        WardUsed(Elements.SingleName(Stats.Hero, ElemSlot.Heavy, el), el, HeroElement.None);
    }

    /// <summary>Earth Shield: the floor in front bursts and a great lump of earth heaves up and over him.</summary>
    private void StartEarthShield()
    {
        float side = Math.Abs(ShieldDir.X) > 0.2f ? Math.Sign(ShieldDir.X) : Facing;
        _moundT = Tune.ElementalWarden.MoundSeconds;
        var at = FloorAt(GlobalPosition + new Vector2(side * Tune.ElementalWarden.MoundReach, 0));
        _mound = new EarthMound { Position = at, Side = side };
        G.Spawn(_mound);
        var col = Elements.ColorOf(HeroElement.Earth);
        G.Fx.AddShake(6f);
        G.Main.Rumble(0.8f, 0.6f, 0.35f);
        G.Fx.Shockwave(at, 64f, new Color(col, 0.9f), 0.45f);
        G.Fx.Debris(at + new Vector2(0, -6), col, 18, 280);
        G.Fx.Dust(at, 14, 2.8f, new Color(0.6f, 0.5f, 0.38f, 0.8f));
        G.Fx.Burst(at + new Vector2(0, -10), new Color(0.5f, 0.4f, 0.3f, 0.9f), 14, 200, 3f, 0.5f, 300);
        G.Sfx.Play("rock", at, 3, 0.05f, 0.5f);
        G.Sfx.Play("rock", at, -2, 0.05f, 0.75f);
        // (what stands where the ground comes up is thrown off it)
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - at;
            if (Math.Abs(to.X) > 26f + e.HitRadius || to.Y < -50f || to.Y > 20f) continue;
            e.Shove(new Vector2(side * 260f, -220f));
        }
        Anim.Punch(new Vector2(0.9f, 1.12f));
    }

    /// <summary>A blow the Earth Shield took: the earth takes it, chips flying.</summary>
    private void MoundStruck(Vector2 from)
    {
        if (_mound != null && IsInstanceValid(_mound)) _mound.Flash = 1f;
        var at = GlobalPosition + new Vector2(0, -10) + (from - GlobalPosition).LimitLength(18f);
        G.Fx.Debris(at, Elements.ColorOf(HeroElement.Earth), 4, 140);
        G.Sfx.Play("rock", at, -6, 0.1f, 1.2f);
    }

    /// <summary>Frost Shield or Wind Barrier: a shell closes about him.</summary>
    private void RaiseShell(HeroElement el)
    {
        _shell = el;
        _shellSoaked = 0f;
        G.Spawn(new WardShell { Hero = this, El = el, Position = GlobalPosition + new Vector2(0, -6) });
        var me = GlobalPosition + new Vector2(0, -6);
        var col = Elements.ColorOf(el);
        if (el == HeroElement.Frost)
        {
            G.Fx.Ring(me, 22, new Color(0.9f, 0.97f, 1f, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
            G.Fx.Burst(me, new Color(0.88f, 0.96f, 1f), 16, 90, 2f, 0.4f, -20);
            G.Sfx.Play("clink", me, -2, 0.05f, 0.7f);
        }
        else
        {
            G.Fx.Ring(me, 22, new Color(col, 0.8f), 0.35f, FxLayer.RingStyle.Dashed);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.Tau / 8f;
                G.Fx.Directional(me + Vector2.Right.Rotated(a) * 20f, Vector2.Right.Rotated(a + 1.6f), 0.2f, new Color(col, 0.7f), 1, 200, 1.6f, 0.3f, 0, 1);
            }
            G.Sfx.Play("dodge", me, -2, 0.05f, 0.8f);
        }
    }

    /// <summary>What a blow leaves after the shell on him has soaked its share (and the shell breaks once it has soaked all it can).</summary>
    private float ShellSoak(float dmg)
    {
        if (_shell == HeroElement.None || dmg <= 0f) return dmg;
        float hold = ShellHold(_shell);
        float soak = Math.Min(dmg * ShellShare(_shell), hold - _shellSoaked);
        _shellSoaked += soak;
        _shellFlash = 1f;
        var me = GlobalPosition + new Vector2(0, -6);
        G.Fx.Burst(me + G.RandDir() * 16f, new Color(Elements.ColorOf(_shell), 0.9f), 4, 60, 1.6f, 0.25f, 0);
        if (_shellSoaked >= hold - 0.01f) BreakShell();
        return dmg - soak;
    }

    /// <summary>The shell has soaked all it can: the ice shatters (frost and a blow to every creature about him), the wind tears apart.</summary>
    private void BreakShell()
    {
        var el = _shell;
        _shell = HeroElement.None;
        var me = GlobalPosition + new Vector2(0, -6);
        if (el == HeroElement.Frost)
        {
            _shellRest = ElemRest(Tune.ElementalWarden.FrostShellRest);
            float r = Tune.ElementalWarden.FrostShellRadius;
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(me) > r + e.HitRadius) continue;
                var away = (e.GlobalPosition - me).Normalized();
                float d = e.Hurt(Tune.ElementalWarden.FrostShellDamage * Stats.DamageMult, away * 160f, e.GlobalPosition, DamageKind.Physical);
                if (d > 0) OnDealtDamage(d);
                if (!e.Dead) e.Build(HeroElement.Frost, Tune.ElementalWarden.FrostShellBuild, Stats.DamageMult, this);
            }
            G.Fx.Burst(me, new Color(0.9f, 0.97f, 1f), 30, 260, 2.6f, 0.55f, 160);
            G.Fx.Ring(me, r, new Color(Elements.ColorOf(HeroElement.Frost), 0.9f), 0.4f, FxLayer.RingStyle.Burst);
            G.Fx.Flash(me, 26, new Color(0.85f, 0.95f, 1f));
            G.Fx.Text(GlobalPosition + new Vector2(0, -32), "SHATTER", Elements.ColorOf(HeroElement.Frost), 12, 0.8f);
            G.Fx.AddShake(3f);
            G.Sfx.Play("clink", me, 2, 0.05f, 0.5f);
            ShellsShattered++;
        }
        else
        {
            _shellRest = ElemRest(Tune.ElementalWarden.WindShellRest);
            var col = Elements.ColorOf(HeroElement.Wind);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.Tau / 12f;
                G.Fx.Directional(me, Vector2.Right.Rotated(a), 0.15f, new Color(col, 0.8f), 1, 300, 1.8f, 0.35f, 0, 1);
            }
            G.Fx.Ring(me, 30, new Color(col, 0.8f), 0.3f, FxLayer.RingStyle.Dashed);
            G.Sfx.Play("dodge", me, 0, 0.05f, 1.3f);
        }
    }
    /// <summary>For the tests: Frost Shields shattered.</summary>
    public int ShellsShattered { get; private set; }

    /// <summary>The mode's guards as time passes: the Earth Shield standing and sinking, the rests.</summary>
    private void TickModeGuards(float dt)
    {
        if (_moundT > 0 && (_moundT -= dt) <= 0)
        {
            _moundRest = ElemRest(Tune.ElementalWarden.MoundRest);
            var at = _mound != null && IsInstanceValid(_mound) ? _mound.GlobalPosition : GlobalPosition;
            G.Fx.Dust(at, 10, 2.4f, new Color(0.6f, 0.5f, 0.38f, 0.7f));
            G.Sfx.Play("rock", at, -4, 0.05f, 0.6f);
        }
        if (_moundRest > 0) _moundRest -= dt;
        if (_shellRest > 0) _shellRest -= dt;
        if (_shellFlash > 0) _shellFlash -= dt * 4f;
        if (Dead && _shell != HeroElement.None) _shell = HeroElement.None;
    }

    /// <summary>For the tests: the guards' rests over and nothing up.</summary>
    public void TestResetGuards() { _moundT = _moundRest = _shellRest = _shellSoaked = 0f; _shell = HeroElement.None; }

    /// <summary>The guards for the other games, in two bits (the Earth Shield first; a shell otherwise).</summary>
    private int GuardBits => _moundT > 0 ? 1 : _shell == HeroElement.Frost ? 2 : _shell == HeroElement.Wind ? 3 : 0;

    /// <summary>A puppet's guards as its game sent them: an Earth Shield or a shell going up here too.</summary>
    private void ReadGuardBits(int bits)
    {
        bool mound = bits == 1;
        var shell = bits == 2 ? HeroElement.Frost : bits == 3 ? HeroElement.Wind : HeroElement.None;
        if (mound && !_netMound)
            G.Spawn(new EarthMound { Position = FloorAt(GlobalPosition + new Vector2(Facing * Tune.ElementalWarden.MoundReach, 0)), Side = Facing });
        if (shell != HeroElement.None && shell != _netShell)
            G.Spawn(new WardShell { Hero = this, El = shell, Position = GlobalPosition + new Vector2(0, -6) });
        _netMound = mound;
        _netShell = shell;
    }

    /// <summary>The guard while it holds: its element about the shield, and the Eye of the Storm's ring of wind.</summary>
    private void TickGuard(float dt)
    {
        var col = _guardB == HeroElement.None ? Elements.ColorOf(_guardA) : Elements.DualColor(_guardA, _guardB);
        var front = GlobalPosition + new Vector2(0, -3) + ShieldDir * 16f;
        if (Engine.GetPhysicsFrames() % 4 == 0) G.Fx.Burst(front + G.RandDir() * 6f, new Color(col, 0.7f), 1, 20, 1.6f, 0.3f, 0, 0);
        if ((_guardTick -= dt) > 0) return;
        _guardTick = 0.1f;
        if (_guard != "Eye of the Storm") return;
        var me = GlobalPosition + new Vector2(0, -4);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - me;
            if (to.Length() > Tune.ElementalWarden.StormRadius + e.HitRadius) continue;
            e.Shove(to.Normalized() * Tune.ElementalWarden.StormShove + new Vector2(0, -40f));
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            if (IsInstanceValid(pr) && !pr.IsQueuedForDeletion() && !pr.Reflected && pr.GlobalPosition.DistanceTo(me) < Tune.ElementalWarden.StormRadius) pr.Deflect();
        for (int k = 0; k < 3; k++)
        {
            float a = G.Range(0f, Mathf.Tau);
            G.Fx.Directional(me + Vector2.Right.Rotated(a) * Tune.ElementalWarden.StormRadius * 0.8f, Vector2.Right.Rotated(a + 1.6f), 0.2f, new Color(col, 0.6f), 1, 180, 1.5f, 0.25f, 0, 1);
        }
    }

    /// <summary>The guard covers all round (Bastion), or the shield's own arc.</summary>
    private bool GuardAllRound => _guard == "Bastion";
    /// <summary>Stone in the guard: nothing gets through, and the shield doesn't wear.</summary>
    private bool GuardStone => _guard != "" && PairHas(_guardA, _guardB, HeroElement.Earth);
    /// <summary>Wind in the guard: shots blocked go back where they came from.</summary>
    private bool GuardReflects => _guard != "" && PairHas(_guardA, _guardB, HeroElement.Wind);

    /// <summary>A melee blow met the guard: its elements answer the striker.</summary>
    private void GuardStruck(Enemy melee)
    {
        if (_guard == "" || melee == null || !IsInstanceValid(melee) || melee.Dead) return;
        var away = (melee.GlobalPosition - GlobalPosition).Normalized();
        float power = Stats.DamageMult;
        if (PairHas(_guardA, _guardB, HeroElement.Earth)) melee.Build(HeroElement.Earth, Tune.Elements.Full, power, this);
        if (PairHas(_guardA, _guardB, HeroElement.Frost))
        {
            melee.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
            // (two of frost: chilled, then frozen solid at once)
            if (_guardA == HeroElement.Frost && _guardB == HeroElement.Frost && !melee.Dead) melee.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
        }
        if (PairHas(_guardA, _guardB, HeroElement.Wind) && !melee.Dead) melee.Shove(away * Tune.ElementalWarden.GuardShove + new Vector2(0, -80f));
        AttributeHit(melee, GlobalPosition);
        var col = _guardB == HeroElement.None ? Elements.ColorOf(_guardA) : Elements.DualColor(_guardA, _guardB);
        G.Fx.Ring(melee.GlobalPosition, melee.HitRadius + 8, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        GuardAnswered++;
    }
    /// <summary>For the tests: melee blows a guard has answered.</summary>
    public int GuardAnswered { get; private set; }

    // ================================================================== the dash

    /// <summary>The Guarded Charge with charges readied: the element's (or the pair's) charge, which runs through creatures.</summary>
    private void WardenDashVersion()
    {
        var (name, a, b) = SpendFor(ElemSlot.Move);
        _wDash = name; _dashA = a; _dashB = b;
        _dashSpeedMult = 1f;
        _dashFrom = GlobalPosition;
        _dashCarryT = 0f;
        _dashDustAt = 0f;
        _carried.Clear();
        switch (name)
        {
            case "Gale Charge": case "Blizzard Charge": case "Duststorm":
                _dashT *= Tune.ElementalWarden.GaleTime; _dashSpeedMult = Tune.ElementalWarden.GaleSpeed; break;
            case "Tempest Charge":
                _dashT *= Tune.ElementalWarden.TempestTime; _dashSpeedMult = Tune.ElementalWarden.GaleSpeed; break;
        }
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        G.Fx.Directional(GlobalPosition, -_dashDir, 0.6f, new Color(col, 0.85f), 12, 220, 2f, 0.3f, 0);
        WardUsed(name, a, b);
    }

    /// <summary>The elemental charge meets what's in its way: each creature once, as the version does; friends passed by a wind get a tailwind.</summary>
    private void WardenDashPass(Vector2 front, float dt)
    {
        float power = Stats.DamageMult;
        bool earth = PairHas(_dashA, _dashB, HeroElement.Earth), frost = PairHas(_dashA, _dashB, HeroElement.Frost), wind = PairHas(_dashA, _dashB, HeroElement.Wind);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(front) > e.HitRadius + 12) continue;
            if (!_dashStruck.Add(e)) continue;
            if (earth)
            {
                e.Interrupt(_dashDir * Tune.Warden.DashPush, Tune.Warden.DashStagger);
                float d = e.Hurt(WardBlow * Tune.ElementalWarden.PloughDamage, _dashDir * 160f, e.GlobalPosition, DamageKind.Physical);
                if (d > 0) OnDealtDamage(d);
            }
            if (frost && !e.Dead)
            {
                bool glacier = _dashA == HeroElement.Frost && _dashB == HeroElement.Frost;
                e.Build(HeroElement.Frost, glacier ? Tune.Elements.Full : wind ? Tune.ElementalWarden.GustBuild : Tune.ElementalWarden.PassBuild, power, this);
                if (glacier && !e.Dead) e.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
            }
            if (wind && !e.Dead)
            {
                if (_wDash == "Tempest Charge") _carried.Add(e);
                else e.Shove(new Vector2(_dashDir.X * Tune.ElementalWarden.AsideShove * 0.4f, -Tune.ElementalWarden.AsideShove));
            }
            AttributeHit(e, front - _dashDir * 10f);
            G.Fx.Spark(e.GlobalPosition, _dashDir, true, Elements.ColorOf(_dashA));
        }
        if (wind)
            foreach (var h in G.Players)
                if (h != this && h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.DistanceTo(front) < 22f) h.GiveBuff(BuffTailwind, Tune.ElementalWarden.TailwindSeconds);
        // the Tempest carries what it took up along in its wake
        if (_carried.Count > 0 && (_dashCarryT -= dt) <= 0)
        {
            _dashCarryT = 0.05f;
            foreach (var e in _carried)
                if (IsInstanceValid(e) && !e.Dead) e.Shove((GlobalPosition + _dashDir * 18f + new Vector2(0, -10f) - e.GlobalPosition) * 8f + _dashDir * Tune.Warden.DashSpeed * _dashSpeedMult * 0.8f);
        }
        // the Duststorm leaves clouds along its path
        if (_wDash == "Duststorm" && GlobalPosition.DistanceTo(_dashFrom) >= _dashDustAt)
        {
            _dashDustAt += Tune.ElementalWarden.DustRadius * 1.2f;
            var cloud = new SmokeCloud { Position = GlobalPosition, Radius = Tune.ElementalWarden.DustRadius * 0.8f, Life = Tune.ElementalWarden.StormDustSeconds };
            G.Spawn(cloud);
            NetSync.HeroVisual(cloud);
            _dusts.Add((GlobalPosition, Tune.ElementalWarden.StormDustSeconds));
        }
        // (its element trailing behind)
        if (Engine.GetPhysicsFrames() % 2 == 0)
            G.Fx.Burst(GlobalPosition + G.RandDir() * 6f, new Color(_dashB == HeroElement.None ? Elements.ColorOf(_dashA) : Elements.DualColor(_dashA, _dashB), 0.8f), 1, 30, 2f, 0.35f, earth ? 300 : 0, 0);
    }

    /// <summary>The elemental charge is over: the quake, the frozen ground, the ice bursting up, the Tempest's fling.</summary>
    private void WardenDashEnd()
    {
        if (_wDash == "") return;
        string name = _wDash;
        _wDash = "";
        var feet = GlobalPosition + new Vector2(0, 12);
        float power = Stats.DamageMult;
        bool earth = PairHas(_dashA, _dashB, HeroElement.Earth), frost = PairHas(_dashA, _dashB, HeroElement.Frost);
        if (earth)
        {
            bool big = name == "Avalanche";
            float r = big ? Tune.ElementalWarden.AvalancheRadius : Tune.ElementalWarden.QuakeRadius;
            float dmg = WardBlow * (big ? Tune.ElementalWarden.AvalancheDamage : Tune.ElementalWarden.QuakeDamage);
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(feet) > r + e.HitRadius) continue;
                var away = new Vector2(Math.Sign(e.GlobalPosition.X - feet.X), -0.8f).Normalized();
                float d = e.Hurt(dmg, away * 120f, e.GlobalPosition, DamageKind.Physical);
                if (d > 0) OnDealtDamage(d);
                if (!e.Dead) { e.Build(HeroElement.Earth, Tune.Elements.Full, power, this); e.Shove(away * 200f); }
            }
            Breakables.SpellBurst(feet, r * 0.5f);
            var col = Elements.ColorOf(HeroElement.Earth);
            G.Fx.Shockwave(feet, r, new Color(col, 0.75f), 0.35f);
            G.Fx.Debris(feet, col.Darkened(0.2f), big ? 18 : 12, 240);
            G.Fx.AddShake(big ? 5f : 3.5f);
            G.Sfx.Play("slam", feet, -2, 0.05f, 0.85f);
            if (big) Launch(new Fissure { Position = FloorAt(GlobalPosition), Dir = Math.Abs(_dashDir.X) > 0.1f ? Math.Sign(_dashDir.X) : Facing, Range = Tune.ElementalWarden.FissureRange, Damage = WardBlow * 1.2f });
        }
        if (name == "Frostquake")
            Launch(new IceEruption { Position = FloorAt(GlobalPosition + new Vector2(Facing * 24f, 0)), Damage = WardBlow * Tune.ElementalWarden.FrostquakeDamage });
        else if (frost)
        {
            // the ground it crossed freezes over
            var from = FloorAt(_dashFrom);
            var to = FloorAt(GlobalPosition);
            float half = Math.Abs(to.X - from.X) * 0.5f + Tune.ElementalWarden.TundraPad;
            Launch(new TundraField { Position = from.Lerp(to, 0.5f), Half = half, Seconds = Tune.ElementalWarden.TundraSeconds });
        }
        if (name == "Tempest Charge")
        {
            foreach (var e in _carried)
                if (IsInstanceValid(e) && !e.Dead) e.Shove(new Vector2(_dashDir.X * Tune.ElementalWarden.TempestFling, -Tune.ElementalWarden.TempestFling * 0.6f));
            G.Fx.Shockwave(GlobalPosition, 60, new Color(Elements.ColorOf(HeroElement.Wind), 0.6f), 0.35f);
            G.Sfx.Play("dodge", GlobalPosition, -2, 0.05f, 0.6f);
            _carried.Clear();
        }
    }
}
