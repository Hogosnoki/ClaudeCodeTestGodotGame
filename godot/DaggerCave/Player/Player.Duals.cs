using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Dual charges (<see cref="Tune.Dual"/>): with two elements readied, the Swordsman's Hard Swing and the Elementalist's Burst spend both
/// for the pair's own ability (<see cref="Elements.DualName"/>), each its own mechanic rather than a bigger blow. The Swordsman's are
/// heaves that do something where the blade lands: Earthsplitter (a fissure runs along the floor), Inferno Cleave (a wall of fire),
/// Glacier Cleaver (a vast axe of ice: it freezes what it cuts and the head shatters it), Frost Heave (ice bursts up under the nearest
/// creature), Magma Rend (a pool of magma that mires) and Thermal Shock (on a creature burning or chilled, a great blow and a cloud of
/// steam to hide in). The Elementalist's: Meteor (falls on a knot of creatures, smashing the platforms it passes), Frozen Orb (a spiral of
/// shards), Tornado (carries creatures along), Frostfire Lance (a beam through a line), Wildfire (fire carried from creature to creature)
/// and Whiteout (a gale that drives back and turns shots round). The spells that last are in <c>Combat/ElementalFx.cs</c>.
/// </summary>
public partial class Player
{
    /// <summary>The swing under way is a dual (its pair), and whether what it does where it lands is done yet.</summary>
    private bool _dualSwing, _dualLanded, _giant;
    private HeroElement _dualA, _dualB;
    /// <summary>The creature a Thermal Shock's blow found burning or chilled (it bursts into steam).</summary>
    private Enemy _thermal;
    /// <summary>The Glacier Cleaver's vast axe of ice is in the hand (for the 3D view).</summary>
    public bool GiantBlade => IsRemote ? (_netFlags & HfGiant) != 0 : _giant && _swingT >= 0;
    /// <summary>For the tests: the last lasting spell a dual made.</summary>
    public HeroSpell LastDualSpell { get; private set; }

    private bool DualIs(HeroElement a, HeroElement b) => _dualSwing && _dualA == a && _dualB == b;

    private void ClearDualSwing() { _dualSwing = false; _dualLanded = false; _giant = false; _thermal = null; }

    private void Used(string what, Color col)
    {
        ElementalUses.Add(what);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), what.ToUpperInvariant(), col, 12, 0.9f);
    }

    private void TickDuals(float dt)
    {
        // (frost smoking off the vast axe)
        if (GiantBlade && Engine.GetPhysicsFrames() % 2 == 0)
        {
            var along = _swingDir != Vector2.Zero ? _swingDir : new Vector2(Facing, -0.3f).Normalized();
            G.Fx.Burst(GlobalPosition + new Vector2(0, -6) + along * G.Range(10f, _swingReach * 0.9f), new Color(0.85f, 0.96f, 1f, 0.8f), 1, 25, 2.2f, 0.35f, 40, 0);
        }
    }

    // ================================================================== the Swordsman

    /// <summary>The Hard Swing with two charges: the pair's heave (its rest by which).</summary>
    private bool DualSword(Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, a, b);
        var col = Elements.DualColor(a, b);
        float rest, dmg, build;
        var swingEl = a;
        bool giant = false;
        switch ((a, b))
        {
            case (HeroElement.Earth, HeroElement.Earth): rest = Tune.Dual.SplitRest; dmg = Tune.Dual.SplitDamage; build = 50f; break;
            case (HeroElement.Fire, HeroElement.Fire): rest = Tune.Dual.WallRest; dmg = Tune.Dual.WallDamage; build = 50f; break;
            case (HeroElement.Frost, HeroElement.Frost): rest = Tune.Dual.AxeRest; dmg = Tune.ElementalSword.HardDamage * Tune.Dual.AxeDamage; build = Tune.Elements.Full; giant = true; break;
            case (HeroElement.Earth, HeroElement.Frost): rest = Tune.Dual.HeaveRest; dmg = Tune.ElementalSword.HardDamage; swingEl = HeroElement.Frost; build = 30f; break;
            case (HeroElement.Earth, HeroElement.Fire): rest = Tune.Dual.PoolRest; dmg = Tune.Dual.PoolDamage; swingEl = HeroElement.Fire; build = 30f; break;
            default: rest = Tune.Dual.ShockRest; dmg = Tune.Dual.ShockDamage; swingEl = HeroElement.Fire; build = 0f; break;
        }
        float scale = Stats.HeaveCooldown / Math.Max(0.01f, Tune.Swordsman.HeaveCooldown);
        _heaveCdTotal = rest * scale;
        _heaveCd = Math.Max(0f, _heaveCd) + _heaveCdTotal;
        _dodgeT = 0;
        float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        Facing = dir;
        StartHeave();
        _swingDmg = BaseDamage * Stats.DamageMult * dmg * Stats.HeaveDamageMult;
        _swingEl = swingEl; _swingBuild = build; _swingShatter = false;
        _dualSwing = true; _dualA = a; _dualB = b;
        if (giant)
        {
            // the blade grows into a vast axe of ice: far longer, wider, slower to raise
            _giant = true;
            _swingReach *= Tune.Dual.AxeReach;
            _swingArc *= Tune.Dual.AxeArc;
            float extra = _windup * (Tune.Dual.AxeWindup - 1f);
            _windup += extra; _heaveRootT += extra;
            G.Fx.Burst(GlobalPosition + new Vector2(Facing * 10, -14), new Color(0.88f, 0.97f, 1f), 16, 140, 2.6f, 0.4f, -40);
            G.Sfx.Play("clink", GlobalPosition, -2, 0.05f, 0.4f);
        }
        G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 22, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        _lastEl = a;
        Used(name, col);
        return true;
    }

    /// <summary>How much more a dual's blow deals on this creature (Thermal Shock, on one burning, chilled or frozen).</summary>
    private float DualBladeMult(Enemy e)
    {
        if (!DualIs(HeroElement.Fire, HeroElement.Frost)) return 1f;
        if (!e.Ignited && !e.Chilled && !e.FrozenSolid) return 1f;
        _thermal = e;
        return 1f + Tune.Dual.ShockBonus;
    }

    /// <summary>A dual's blow landed: what the pair does to the creature beyond the swing's own element.</summary>
    private void DualBladeHit(Enemy e)
    {
        if (!_dualSwing || e.Dead) return;
        if (DualIs(HeroElement.Frost, HeroElement.Frost)) e.Build(HeroElement.Frost, Tune.Elements.Full, Stats.DamageMult, this);
        else if (DualIs(HeroElement.Fire, HeroElement.Frost))
        {
            if (_thermal == e) ThermalShock(e);
            else { e.Build(HeroElement.Fire, Tune.Elements.Full * 0.5f, Stats.DamageMult, this); e.Build(HeroElement.Frost, Tune.Elements.Full * 0.5f, Stats.DamageMult, this); }
        }
    }

    /// <summary>Fire meets frost in a creature: it cracks, its fire and ice go at once, and it bursts into a cloud of steam that hides the heroes in it.</summary>
    private void ThermalShock(Enemy e)
    {
        var at = e.GlobalPosition;
        e.Quench();
        e.Thaw();
        G.Fx.Text(at + new Vector2(0, -e.HitRadius - 16), "THERMAL SHOCK", new Color(1f, 0.85f, 0.85f), 11, 0.9f);
        G.Fx.Burst(at, new Color(0.95f, 0.95f, 0.95f, 0.8f), 20, 160, 3.4f, 0.7f, -60);
        G.Fx.Ring(at, Tune.Dual.SteamRadius, new Color(0.95f, 0.95f, 1f, 0.8f), 0.35f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("lava", at, -2, 0.05f, 1.4f);
        var steam = new SmokeCloud { Position = at, Radius = Tune.Dual.SteamRadius, Life = Tune.Dual.SteamSeconds };
        G.Spawn(steam);
        NetSync.HeroVisual(steam);
        ElementalUses.Add("Thermal Shock!");
    }

    /// <summary>A dual heave's blade meets the floor: the pair's lasting part begins there.</summary>
    private void DualImpact()
    {
        if (!_dualSwing || _dualLanded) return;
        _dualLanded = true;
        var at = GlobalPosition + new Vector2(Facing * _swingReach * 0.75f, 12);
        if (!Ground.Below(at + new Vector2(0, -20), 70, out var floor) && !Ground.Below(GlobalPosition, 40, out floor)) floor = GlobalPosition + new Vector2(0, 12);
        float blow = BaseDamage * Stats.DamageMult;
        HeroSpell spell = null;
        switch ((_dualA, _dualB))
        {
            case (HeroElement.Earth, HeroElement.Earth):
                spell = new Fissure { Position = floor, Dir = Facing, Damage = blow * Tune.Dual.SplitDamage };
                G.Fx.Debris(floor, Elements.ColorOf(HeroElement.Earth), 12, 260);
                break;
            case (HeroElement.Fire, HeroElement.Fire):
                spell = new FireWall { Position = floor, TickDamage = blow * Tune.Dual.WallTickDamage };
                G.Fx.Explosion(floor + new Vector2(0, -10), new Color(1f, 0.5f, 0.15f), 0.8f);
                break;
            case (HeroElement.Frost, HeroElement.Frost):
            {
                // the axe head comes down: what its blow froze bursts
                var col = Elements.ColorOf(HeroElement.Frost);
                foreach (var e in G.Enemies.ToArray())
                    if (!e.Dead && e.FrozenSolid && e.GlobalPosition.DistanceTo(at) < _swingReach * 0.6f + e.HitRadius) e.Shatter(Stats.DamageMult * 2f, this);
                G.Fx.Directional(floor, Vector2.Up, 0.9f, new Color(0.9f, 0.97f, 1f), 22, 320, 3f, 0.5f, 500, 0);
                G.Fx.Shockwave(floor, 60, new Color(col, 0.7f), 0.35f);
                G.Fx.AddShake(6f);
                Breakables.SpellBurst(at, 40f);
                break;
            }
            case (HeroElement.Earth, HeroElement.Frost):
            {
                // the ground bursts up under the nearest creature in reach (or before the blade, if there is none)
                Enemy best = null;
                float bestD = Tune.Dual.HeaveRange;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit) continue;
                    float d = e.GlobalPosition.DistanceTo(GlobalPosition);
                    if (d < bestD) { bestD = d; best = e; }
                }
                var spot = floor;
                if (best != null && Ground.Below(best.GlobalPosition, 260, out var under)) spot = under;
                else if (best != null) spot = best.GlobalPosition + new Vector2(0, best.HitRadius);
                spell = new IceEruption { Position = spot, Damage = blow * Tune.Dual.HeaveDamage };
                // (a seam of frost runs through the ground to it)
                G.Fx.Beam(floor, spot, new Color(Elements.ColorOf(HeroElement.Frost), 0.55f));
                break;
            }
            case (HeroElement.Earth, HeroElement.Fire):
                spell = new MagmaPool { Position = floor, TickDamage = blow * Tune.Dual.PoolTickDamage };
                break;
            default:
                G.Fx.Burst(floor + new Vector2(0, -6), new Color(0.95f, 0.95f, 0.95f, 0.7f), 10, 90, 3f, 0.6f, -50);
                break;
        }
        if (spell == null) return;
        spell.Caster = this;
        spell.Power = Stats.DamageMult;
        G.Spawn(spell);
        LastDualSpell = spell;
    }

    // ================================================================== the Elementalist

    /// <summary>Burst with two charges: the pair's spell (its rest by which).</summary>
    private bool DualStaff(Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, a, b);
        var col = Elements.DualColor(a, b);
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        float power = Stats.DamageMult;
        float rest = (a, b) switch
        {
            (HeroElement.Fire, HeroElement.Fire) => Tune.Dual.MeteorRest,
            (HeroElement.Frost, HeroElement.Frost) => Tune.Dual.OrbRest,
            (HeroElement.Wind, HeroElement.Wind) => Tune.Dual.TornadoRest,
            (HeroElement.Fire, HeroElement.Frost) => Tune.Dual.LanceRest,
            (HeroElement.Fire, HeroElement.Wind) => Tune.Dual.WildRest,
            _ => Tune.Dual.GaleRest,
        };
        _snapCd = _snapCdTotal = rest;
        _snapOwes = true;
        LastCast = a == HeroElement.Fire ? "cinder" : "snap";
        _castGlow = 1f;
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        CastDir = aim;
        Anim.Face((int)Facing, instant: true);
        HeroSpell spell = null;
        switch ((a, b))
        {
            case (HeroElement.Fire, HeroElement.Fire):
            {
                var target = MeteorTarget(aim);
                // it comes down slantwise out of the dark overhead (from as high as the roof allows)
                var from = new Vector2(-Facing * 0.45f, -1f).Normalized();
                G.Cave.Raycast(target + new Vector2(0, -8), from, Tune.Dual.MeteorHeight, out var top, 4f);
                var start = top - from * 8f;
                if (start.DistanceTo(target) < 60f) start = target + from * 60f;
                spell = new Meteor { Position = start, Target = target, Damage = Tune.Dual.MeteorDamage * power };
                break;
            }
            case (HeroElement.Frost, HeroElement.Frost):
            {
                var t = FindSpellTarget(aim, Tune.Elementalist.BoltRange, 40f);
                var d = t != null ? (t.GlobalPosition - CastPoint).Normalized() : aim;
                spell = new FrozenOrb { Position = CastPoint + d * 10f, Dir = d };
                break;
            }
            case (HeroElement.Wind, HeroElement.Wind):
            {
                if (!Ground.Below(GlobalPosition, 60, out var floor)) floor = GlobalPosition + new Vector2(0, 12);
                spell = new Tornado { Position = floor + new Vector2(Facing * 18f, 0), Dir = Facing };
                break;
            }
            case (HeroElement.Fire, HeroElement.Frost):
                FrostfireLance(aim, power);
                break;
            case (HeroElement.Fire, HeroElement.Wind):
                spell = new Wildfire { Position = GlobalPosition };
                G.Fx.Shockwave(GlobalPosition, 120, new Color(1f, 0.6f, 0.25f, 0.5f), 0.4f);
                break;
            default:
                spell = new Whiteout { Position = CastPoint, Dir = aim };
                break;
        }
        if (spell != null)
        {
            spell.Caster = this;
            spell.Power = power;
            G.Spawn(spell);
            LastDualSpell = spell;
        }
        G.Fx.Ring(CastPoint, 18, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        Anim.Once("rupture", 3, 1.6f);
        G.Sfx.Play(a == HeroElement.Fire ? "lava" : a == HeroElement.Frost ? "clink" : "dodge", GlobalPosition, -4, 0.05f, 0.7f);
        G.Main.Rumble(0.4f, 0.4f, 0.14f);
        _lastEl = a;
        Used(name, col);
        return true;
    }

    /// <summary>Where a meteor falls: on the creature in view with the most others close about it, or ahead along the aim.</summary>
    private Vector2 MeteorTarget(Vector2 aim)
    {
        var foes = InView().ToList();
        Enemy best = null;
        int most = -1;
        foreach (var e in foes)
        {
            int n = foes.Count(o => o.GlobalPosition.DistanceTo(e.GlobalPosition) < Tune.Dual.MeteorRadius);
            if (n > most) { most = n; best = e; }
        }
        if (best != null) return best.GlobalPosition;
        var ahead = CastPoint + aim * 200f;
        G.Cave.Raycast(CastPoint, aim, 200f, out ahead, 4f);
        return Ground.Below(ahead, 200, out var floor) ? floor : ahead;
    }

    /// <summary>Frostfire Lance: fire and frost together in one beam, through every creature in its line; one already burning or chilled
    /// takes far more.</summary>
    private void FrostfireLance(Vector2 aim, float power)
    {
        var t = FindSpellTarget(aim, Tune.Dual.LanceRange, 30f);
        var dir = t != null ? (t.GlobalPosition - CastPoint).Normalized() : aim;
        CastDir = dir;
        var from = CastPoint;
        G.Cave.Raycast(from, dir, Tune.Dual.LanceRange, out var end, 4f);
        Breakables.Spell(from, end, 8f);
        int struck = 0;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var on = Geometry2D.GetClosestPointToSegment(e.GlobalPosition, from, end);
            if (on.DistanceTo(e.GlobalPosition) > e.HitRadius + 6f) continue;
            bool shock = e.Ignited || e.Chilled || e.FrozenSolid;
            float dealt = e.Hurt(Tune.Dual.LanceDamage * power * (shock ? Tune.Dual.LanceBonus : 1f), dir * 60f, on, DamageKind.Fire);
            if (dealt > 0) { OnDealtDamage(dealt); e.HitStop(Tune.Feel.HitStopBolt); }
            struck++;
            if (shock) G.Fx.Text(e.GlobalPosition + new Vector2(0, -e.HitRadius - 14), "THERMAL SHOCK", new Color(1f, 0.85f, 0.85f), 10, 0.7f);
            if (e.Dead) continue;
            e.Build(HeroElement.Fire, Tune.Dual.LanceBuild, power, this);
            e.Build(HeroElement.Frost, Tune.Dual.LanceBuild, power, this);
        }
        // two beams twisted round each other, steam boiling off along them
        var perp = dir.Orthogonal() * 2.5f;
        G.Fx.Beam(from + perp, end + perp, new Color(1f, 0.5f, 0.15f));
        G.Fx.Beam(from - perp, end - perp, Elements.ColorOf(HeroElement.Frost));
        float len = from.DistanceTo(end);
        for (float s = 12f; s < len; s += 22f) G.Fx.Burst(from + dir * s, new Color(0.95f, 0.95f, 0.95f, 0.6f), 1, 30, 3f, 0.5f, -60);
        G.Fx.Flash(end, 16, new Color(1f, 0.85f, 0.8f), 0.12f);
        G.Fx.AddShake(2.5f);
        if (struck == 0) G.Fx.Burst(end, new Color(1f, 0.6f, 0.3f), 8, 120, 2f, 0.3f);
    }
}
