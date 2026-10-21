using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Dual charges (<see cref="Tune.Dual"/>): with two elements readied, a button that has an ability for the pair spends both on it
/// (<see cref="Elements.DualName"/>), each its own mechanic rather than a bigger blow. The Swordsman's are on the Hard Swing, heaves that do
/// something where the blade lands: Earthsplitter (a fissure runs along the floor), Inferno Cleave (a wall of fire), Glacier Cleaver (a
/// vast axe of ice: it freezes what it cuts and the head shatters it), Frost Heave (ice bursts up under the nearest creature), Magma Rend
/// (a pool of magma that mires) and Thermal Shock (on a creature burning or chilled, a great blow and a cloud of steam to hide in). The
/// Elementalist's are on three buttons: the bolt (Meteorite, Cryometeor, Fire Barrage, Frost Barrage), movement (Flamebreath, Frost Tower,
/// Fire Barrier, Frost Barrier) and the fixed-range spell (Rain of Fire, Hailstorm, Fire Storm, Blizzard, Tornado, Frostfire Lance). The
/// spells that last are in <c>Combat/ElementalFx.cs</c>.
/// </summary>
public partial class Player
{
    /// <summary>The swing under way is a dual (its pair), and whether what it does where it lands is done yet.</summary>
    private bool _dualSwing, _dualLanded, _giant;
    /// <summary>An Earthsplitter or Breaking Quake winding up: the ground about him trembling, rock lifting off it.</summary>
    private bool _quake;
    private HeroElement _dualA, _dualB;
    /// <summary>The Glacier Cleaver's vast axe of ice is in the hand (for the 3D view).</summary>
    public bool GiantBlade => IsRemote ? (_netFlags & HfGiant) != 0 : _giant && _swingT >= 0;
    /// <summary>For the tests: the last Frost Tower raised.</summary>
    public FrostTower LastTower { get; private set; }
    /// <summary>For the tests: the last lasting spell an element (or a pair) made.</summary>
    public HeroSpell LastElementSpell { get; private set; }

    private bool DualIs(HeroElement a, HeroElement b) => _dualSwing && _dualA == a && _dualB == b;

    private void ClearDualSwing() { _dualSwing = false; _dualLanded = false; _giant = false; _quake = false; }

    private void Used(string what, Color col)
    {
        ElementalUses.Add(what);
        G.Fx.AbilityName(GlobalPosition + new Vector2(0, -32), what.ToUpperInvariant(), col, 12, 0.9f);
    }

    private void TickDuals(float dt)
    {
        // (an Earthsplitter or Breaking Quake gathering: the ground trembles harder and harder, grit and stones lifting off it about him)
        if (_quake && _swingT >= 0 && SweepT < 0)
        {
            float k = Math.Clamp(_swingT / Math.Max(0.01f, _windup), 0f, 1f);
            var feet = GlobalPosition + new Vector2(0, 12);
            G.Fx.AddShake(0.6f + 2.4f * k);
            if (Engine.GetPhysicsFrames() % 3 == 0)
            {
                var spot = feet + new Vector2(G.Range(-40f, 40f) * (0.5f + k), 0);
                var col = _dualB == HeroElement.Fire ? new Color(1f, 0.5f, 0.15f) : Elements.ColorOf(HeroElement.Earth);
                G.Fx.Directional(spot, Vector2.Up, 0.3f, G.Chance(0.5f) ? col : col.Darkened(0.4f), 1 + (int)(2 * k), G.Range(60, 120 + 160 * k), G.Range(2f, 3.4f), 0.5f, 260, 0);
                if (G.Chance(0.4f)) G.Fx.Dust(spot, 1, 1f + k, new Color(0.55f, 0.46f, 0.36f, 0.6f));
            }
            if (Engine.GetPhysicsFrames() % 20 == 0) { G.Sfx.Play("rock", feet, -10 + 6 * k, 0.1f, 0.5f + 0.2f * k); G.Main.Rumble(0.2f + 0.4f * k, 0.2f + 0.4f * k, 0.1f); }
        }
        // (frost smoking off the vast axe, once it is grown)
        if (GiantBlade && AxeGrow > 0.6f && Engine.GetPhysicsFrames() % 2 == 0)
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
        string name = Elements.DualName(Stats.Hero, ElemSlot.Heavy, a, b);
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
            case (HeroElement.Earth, HeroElement.Fire): rest = Tune.Dual.QuakeRest; dmg = Tune.Dual.QuakeDamage; swingEl = HeroElement.Fire; build = 0f; break;
            default: rest = Tune.Dual.ShockRest; dmg = Tune.Dual.ShockDamage; swingEl = HeroElement.Fire; build = 0f; break;
        }
        float scale = Stats.HeaveCooldown / Math.Max(0.01f, Tune.Swordsman.HeaveCooldown);
        rest = ElemRest(rest);
        if (rest > 0f) { _heaveCdTotal = rest * scale; _heaveCd = Math.Max(0f, _heaveCd) + _heaveCdTotal; }
        _dodgeT = 0;
        float dir = Math.Abs(aim.X) > 0.2f ? Math.Sign(aim.X) : Facing;
        Facing = dir;
        StartHeave();
        _swingDmg = BaseDamage * Stats.DamageMult * dmg * Stats.HeaveDamageMult;
        _swingEl = swingEl; _swingBuild = build; _swingShatter = false;
        _dualSwing = true; _dualA = a; _dualB = b;
        if (a == HeroElement.Earth && b is HeroElement.Earth or HeroElement.Fire)
        {
            // Earthsplitter and Breaking Quake gather a long while (as the axe does), the ground trembling, before the blade is driven down
            float extra = _windup * ((b == HeroElement.Earth ? Tune.Dual.SplitWindup : Tune.Dual.QuakeWindup) - 1f);
            _windup += extra; _heaveRootT += extra;
            _quake = true;
            G.Sfx.Play("rock", GlobalPosition, -4, 0.05f, 0.5f);
        }
        if (giant)
        {
            // the Ice Axe's swing, but the head that grows out of the blade is vast and ornate: far longer, wider, slower to grow
            _giant = true;
            _swingReach *= Tune.Dual.AxeReach;
            StartAxe(2, Tune.Dual.AxeWindup);
            G.Fx.Burst(GlobalPosition + new Vector2(Facing * 10, -14), new Color(0.88f, 0.97f, 1f), 16, 140, 2.6f, 0.4f, -40);
            G.Sfx.Play("clink", GlobalPosition, -2, 0.05f, 0.4f);
        }
        G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 22, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        _lastEl = a;
        Used(name, col);
        return true;
    }

    /// <summary>A dual's blow landed: what the pair does to the creature beyond the swing's own element.</summary>
    private void DualBladeHit(Enemy e)
    {
        if (!_dualSwing || e.Dead) return;
        if (DualIs(HeroElement.Frost, HeroElement.Frost)) e.Build(HeroElement.Frost, Tune.Elements.Full, Stats.DamageMult, this);
        else if (DualIs(HeroElement.Fire, HeroElement.Frost)) ThermalShock(e);
    }

    /// <summary>Which of fire and frost a creature holds more of (Steam Strike and Thermal Shock add that one): its build-ups, burning counting
    /// as a full meter of fire, chilled as one of frost and frozen solid as two. Even, the hero's mode decides.</summary>
    private HeroElement HigherOf(Enemy e)
    {
        float full = Tune.Elements.Full;
        float fire = e.FireBuild + (e.Ignited ? full : 0f);
        float frost = e.FrostBuild + (e.FrozenSolid ? full * 2f : e.Chilled ? full : 0f);
        if (Math.Abs(fire - frost) < 0.5f) return Mode == HeroElement.Frost ? HeroElement.Frost : HeroElement.Fire;
        return fire > frost ? HeroElement.Fire : HeroElement.Frost;
    }

    /// <summary>Fire meets frost in a creature: a full build-up of whichever it holds more of, and then, frozen solid it shatters, burning it
    /// bursts, there and then, in a puff of steam.</summary>
    private void ThermalShock(Enemy e)
    {
        var at = e.GlobalPosition;
        var el = HigherOf(e);
        e.Build(el, Tune.Elements.Full, Stats.DamageMult, this);
        bool burst = !e.Dead && (e.FrozenSolid ? e.Shatter(Stats.DamageMult * 2f, this) : e.BurstAlight(Stats.DamageMult, this));
        G.Fx.Burst(at, new Color(0.95f, 0.95f, 0.95f, 0.8f), 14, 140, 3.2f, 0.6f, -60);
        G.Fx.Ring(at, e.HitRadius + 14, new Color(Elements.ColorOf(el), 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Sfx.Play("lava", at, -4, 0.05f, 1.4f);
        if (burst) ElementalUses.Add("Thermal Shock!");
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
                // the blade comes down at last: the whole floor jolts, rock flies, and the crack tears away along it
                spell = new Fissure { Position = floor, Dir = Facing, Damage = blow * Tune.Dual.SplitDamage, Dramatic = true };
                G.Fx.Debris(floor, Elements.ColorOf(HeroElement.Earth), 26, 360);
                G.Fx.Debris(floor, Elements.ColorOf(HeroElement.Earth).Darkened(0.35f), 14, 240);
                G.Fx.Shockwave(floor, 70, new Color(Elements.ColorOf(HeroElement.Earth), 0.75f), 0.4f);
                G.Fx.Dust(floor, 16, 3f);
                G.Fx.AddShake(9f);
                G.Main.Rumble(0.9f, 1f, 0.35f);
                G.Sfx.Play("slam", floor, 1, 0.05f, 0.6f);
                foreach (var e in G.Enemies.ToArray()) if (!e.Dead && e.GlobalPosition.DistanceTo(floor) < 160f) e.HitStop(0.1f);
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
                // Breaking Quake: the blade is driven into the floor, and great cracks full of magma tear out from it either way
                spell = new MagmaCracks { Position = floor, Half = Tune.Dual.QuakeHalf, Seconds = Tune.Dual.QuakeSeconds, Damage = blow * Tune.Dual.QuakeCrackDamage, Seed = G.RangeI(0, 99999) };
                G.Fx.Flash(floor + new Vector2(0, -6), 30, new Color(1f, 0.55f, 0.15f), 0.15f);
                foreach (var e in G.Enemies.ToArray()) if (!e.Dead && e.GlobalPosition.DistanceTo(floor) < Tune.Dual.QuakeHalf + e.HitRadius) e.HitStop(0.12f);
                break;
            default:
                G.Fx.Burst(floor + new Vector2(0, -6), new Color(0.95f, 0.95f, 0.95f, 0.7f), 10, 90, 3f, 0.6f, -50);
                break;
        }
        if (spell == null) return;
        spell.Caster = this;
        spell.Power = Stats.DamageMult;
        G.Spawn(spell);
        LastElementSpell = spell;
        // (those with a body of their own show in the others' games too)
        NetSync.HeroVisual(spell);
    }

    // ================================================================== the Elementalist

    /// <summary>The pair's spell is cast: its name over the hero, the staff's glow, the cast's sound and pose.</summary>
    private void CastDual(string name, Color col, HeroElement a)
    {
        G.Fx.Ring(CastPoint, 18, new Color(col, 0.95f), 0.35f, FxLayer.RingStyle.Burst);
        Anim.Once("rupture", 3, 1.6f);
        G.Sfx.Play(a == HeroElement.Fire ? "lava" : a == HeroElement.Frost ? "clink" : "dodge", GlobalPosition, -4, 0.05f, 0.7f);
        G.Main.Rumble(0.4f, 0.4f, 0.14f);
        _castGlow = 1f;
        _lastEl = a;
        Used(name, col);
    }

    private void Launch(HeroSpell spell)
    {
        spell.Caster = this;
        spell.Power = Stats.DamageMult;
        G.Spawn(spell);
        LastElementSpell = spell;
        // (those with a body of their own show in the others' games too)
        NetSync.HeroVisual(spell);
    }

    /// <summary>The attack with two charges: Meteorite, Cryometeor, Fire Barrage, Frost Barrage or Strong Gust (each rests the pair's bolt a while).</summary>
    private bool DualBolt(Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Attack, a, b);
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        if (Math.Abs(aim.X) > 0.15f) Facing = Math.Sign(aim.X);
        CastDir = aim;
        Anim.Face((int)Facing, instant: true);
        float power = Stats.DamageMult;
        float rest = Tune.Dual.BarrageRest;
        _boltCd = Tune.ElementalStaff.ShotGap;
        AttacksStarted++;
        if (a == HeroElement.Wind && b == HeroElement.Wind)
        {
            // Strong Gust: a little whirlwind off the staff, racing along the floor and shoving everything it meets back
            rest = Tune.Dual.GustRest;
            float dir = Math.Abs(aim.X) > 0.15f ? Math.Sign(aim.X) : Facing;
            if (!Ground.Below(GlobalPosition, 40, out var floor)) floor = GlobalPosition + new Vector2(0, 13);
            Launch(new StrongGust { Position = floor + new Vector2(dir * 10f, 0), Dir = dir });
            CastSlow(0.3f);
        }
        else if (a == HeroElement.Fire && b == HeroElement.Frost)
        {
            // Frostfire Bolts: a bolt of fire and a bolt of frost loosed together, fanned a little apart
            rest = Tune.Dual.BarrageRest;
            var t = FindSpellTarget(aim, Tune.Elementalist.BoltRange, Tune.Elementalist.BoltConeDegrees);
            var dir = t != null ? (t.GlobalPosition - CastPoint).Normalized() : aim;
            LooseBolt(dir.Rotated(-0.08f), HeroElement.Fire, Tune.ElementalStaff.ShotDamage, Tune.Elementalist.BoltRange);
            LooseBolt(dir.Rotated(0.08f), HeroElement.Frost, Tune.ElementalStaff.ShotDamage, Tune.Elementalist.BoltRange);
        }
        else if (a == b)
        {
            // Meteorite / Cryometeor: a rock out of the dark overhead, onto the thickest knot of creatures along the aim
            rest = Tune.Dual.MeteorRest;
            var target = MeteorTarget(aim);
            var from = new Vector2(-Facing * 0.45f, -1f).Normalized();
            // standing, it falls through any rock above your feet (it comes out of the roof itself) and bursts only on ground as low as
            // you stand or lower; in the air, any ground will do, so it starts below the roof
            bool standing = IsOnFloor();
            Vector2 start;
            if (standing) start = target + from * Tune.Dual.MeteorHeight;
            else
            {
                G.Cave.Raycast(target + new Vector2(0, -8), from, Tune.Dual.MeteorHeight, out var top, 4f);
                start = top - from * 8f;
                if (start.DistanceTo(target) < 60f) start = target + from * 60f;
            }
            bool frost = a == HeroElement.Frost;
            Launch(new Meteor { Position = start, Target = target, Frost = frost, GroundY = standing ? GlobalPosition.Y + 13f : null,
                Damage = (frost ? Tune.Dual.CryoDamage : Tune.Dual.MeteorDamage) * power });
            CastSlow(0.4f);
        }
        else
        {
            // Fire Barrage / Frost Barrage: the wind lends the staff a volley, fanned along the aim
            var el = a == HeroElement.Wind ? b : a;
            Launch(new Barrage { Position = CastPoint, El = el, Aim = aim });
            CastSlow(Tune.Dual.BarrageBolts * Tune.Dual.BarrageEvery + 0.15f);
        }
        _dualBoltCd = _dualBoltTotal = ElemRest(rest);
        LastCast = a == HeroElement.Frost ? "frost" : "fire";
        CastDual(name, Elements.DualColor(a, b), a);
        return true;
    }

    /// <summary>A bolt of a barrage, from the Barrage spell.</summary>
    public void BarrageBolt(HeroElement el, Vector2 aim)
    {
        var t = FindSpellTarget(aim, Tune.Elementalist.BoltRange, 50f);
        var dir = (t != null ? (t.GlobalPosition - CastPoint).Normalized() : aim).Rotated(Mathf.DegToRad(G.Range(-Tune.Dual.BarrageSpread, Tune.Dual.BarrageSpread)));
        CastDir = dir;
        _castGlow = 0.8f;
        var bolt = new ElementBolt
        {
            Position = CastPoint + dir * 6f, Dir = dir, Range = Tune.Elementalist.BoltRange, Caster = this,
            Frost = el == HeroElement.Frost, BuildMult = 0.5f,
            Damage = Tune.Elementalist.FireDamage * Tune.Dual.BarrageDamage * Stats.DamageMult * Stats.PrimaryDamageMult,
        };
        G.Spawn(bolt);
        NetSync.HeroVisual(bolt);
        G.Sfx.Play(el == HeroElement.Frost ? "clink" : "throw", CastPoint, -12, 0.1f, 1.7f);
    }

    /// <summary>The dodge with something on top: Flamebreath, Frost Tower, Sky Lift (greater), Fire Barrier, Frost Barrier, Steam Veil (mixes).
    /// Returns the hero's motion from here.</summary>
    private Vector2 DualMove(Vector2 v, float jumpV, Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Move, a, b);
        float rest;
        if (name == "Sky Lift")
        {
            // Sky Lift: a great push of air, carrying you high, and a wind at the back of every friend near
            rest = Tune.Dual.BreathRest;
            v.Y = -jumpV * 1.7f;
            foreach (var h in HeroesNear(Tune.ElementalStaff.AidRange)) h.GiveBuff(BuffTailwind, Tune.ElementalStaff.TailwindSeconds);
            G.Fx.Directional(GlobalPosition + new Vector2(0, 12), Vector2.Down, 0.7f, new Color(Elements.ColorOf(HeroElement.Wind), 0.85f), 18, 220, 2.2f, 0.4f, 0, 0);
            G.Fx.Ring(GlobalPosition, Tune.ElementalStaff.AidRange * 0.5f, new Color(Elements.ColorOf(HeroElement.Wind), 0.7f), 0.4f, FxLayer.RingStyle.Dashed);
        }
        else if (name == "Steam Veil")
        {
            // Steam Veil: fire and frost meet about you in a burst of steam: friends in the cloud are hidden, creatures in the burst burn and chill
            rest = Tune.Dual.BarrierRest;
            var cloud = new SmokeCloud { Position = GlobalPosition, Radius = 56f, Life = 5f };
            G.Spawn(cloud);
            NetSync.HeroVisual(cloud);
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(GlobalPosition) > 56f + e.HitRadius) continue;
                e.Build(HeroElement.Fire, 30f, Stats.DamageMult, this);
                if (!e.Dead) e.Build(HeroElement.Frost, 30f, Stats.DamageMult, this);
            }
            G.Fx.Burst(GlobalPosition, new Color(0.95f, 0.95f, 0.95f, 0.8f), 20, 160, 3.4f, 0.7f, -60);
        }
        else if (a == b && a == HeroElement.Fire)
        {
            // Flamebreath: fire pours from the staff (aimed down, it holds you up)
            rest = Tune.Dual.BreathRest;
            Launch(new FlameBreath { Position = CastPoint, Dir = aim.Y < -0.9f ? new Vector2(Facing, 0.2f).Normalized() : aim });
            CastSlow(Tune.Dual.BreathSeconds);
        }
        else if (a == b)
        {
            // Frost Tower: you leap, and a pillar of ice rises under you to stand and cast from
            rest = Tune.Dual.TowerRest;
            if (!Ground.Below(GlobalPosition, 60, out var floor)) floor = GlobalPosition + new Vector2(0, 13);
            v.Y = -jumpV * Tune.Dual.TowerJump;
            var tower = new FrostTower { Position = floor, Caster = this, MaxHp = Tune.Dual.TowerHp * Stats.MaxHp / Math.Max(1f, Tune.Elementalist.StartHp) };
            G.Spawn(tower);
            LastTower = tower;
        }
        else if (a == HeroElement.Fire)
        {
            // Fire Barrier: a ring of fire about you, going where you go
            rest = Tune.Dual.RingRest;
            Launch(new FireRing { Position = GlobalPosition });
        }
        else
        {
            // Frost Barrier: frost armour, waiting for the next melee strike
            rest = Tune.Dual.BarrierRest;
            GiveBuff(BuffFrostWard, Tune.Dual.WardSeconds);
            G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 22, new Color(Elements.ColorOf(HeroElement.Frost), 0.95f), 0.4f, FxLayer.RingStyle.Burst);
            G.Fx.Burst(GlobalPosition + new Vector2(0, -4), new Color(0.9f, 0.97f, 1f), 12, 80, 2f, 0.4f, 0);
        }
        _jumpElemRest = _jumpRestTotal = ElemRest(rest);
        CastDual(name, Elements.DualColor(a, b), a);
        return v;
    }

    /// <summary>The Burst button with two charges: Rain of Fire, Hailstorm, Fire Storm, Blizzard, Tornado or Frostfire Lance.</summary>
    private bool DualFixed(Vector2 aim)
    {
        var (a, b) = SpendPair();
        string name = Elements.DualName(Stats.Hero, ElemSlot.Heavy, a, b);
        var spot = FixedSpot(aim);
        float power = Stats.DamageMult;
        float rest;
        switch ((a, b))
        {
            case (HeroElement.Fire, HeroElement.Fire):
            case (HeroElement.Frost, HeroElement.Frost):
                // Rain of Fire / Hailstorm: it falls out of the dark over the spot
                rest = Tune.Dual.RainRest;
                Launch(new Downpour { Position = spot, El = a });
                break;
            case (HeroElement.Fire, HeroElement.Wind):
                rest = Tune.Dual.StormRest;
                SpawnStorm(spot, HeroElement.Fire, Tune.Elementalist.FirestormDamage, Tune.ElementalStaff.StormFireBuild, Tune.Dual.StormPull);
                break;
            case (HeroElement.Frost, HeroElement.Wind):
                rest = Tune.Dual.StormRest;
                SpawnStorm(spot, HeroElement.Frost, Tune.Elementalist.BlizzardDamage, Tune.ElementalStaff.StormFrostBuild, Tune.Dual.StormPull);
                break;
            case (HeroElement.Wind, HeroElement.Wind):
            {
                rest = Tune.Dual.TornadoRest;
                if (!Ground.Below(GlobalPosition, 60, out var floor)) floor = GlobalPosition + new Vector2(0, 12);
                Launch(new Tornado { Position = floor + new Vector2(Facing * 18f, 0), Dir = Facing });
                break;
            }
            default:
                rest = Tune.Dual.LanceRest;
                FrostfireLance(aim, power);
                break;
        }
        if (Tune.Elements.VersionsRest) { _snapCd = _snapCdTotal = rest; _snapOwes = true; }
        LastCast = a == HeroElement.Fire ? "cinder" : "snap";
        CastDual(name, Elements.DualColor(a, b), a);
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
        CastSlow();
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
