using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Elementalist's kit. Every spell costs alimus, which comes back by itself (its only
/// source); the bolts are free. The attack button hurls a firebolt that can set a creature
/// alight (Frostbolt: quicker, weaker bolts of frost that slow, and now and then freeze a
/// creature solid). The dodge button raises an Updraft, a column of air in which every
/// hero has a fraction of gravity and a fraction of the fall speed (floating up on a jump, drifting down). The ability button calls down a Blizzard at the aim point: nine
/// small strikes that can each freeze a creature (Firestorm: fire that sets them alight). The
/// second ability snaps: every frozen creature in view shatters, hurting whatever is near it
/// (Cinder Snap: every burning creature bursts). Alterations: Frostbolt, Narrow Draft (a taller,
/// narrower, longer updraft), Firestorm and Cinder Snap.
/// </summary>
public partial class Player
{
    private bool IsElementalist => Stats.Hero == HeroKind.Elementalist;

    /// <summary>The Elementalist's reserve: spells cost it, and it comes back by itself.</summary>
    public float Alimus { get; private set; }
    private float _boltCd, _updraftCd, _snapCd, _snapAt = -1;
    private List<Enemy> _snapping;

    /// <summary>Bolts cast, blizzards called, snaps made (for the tests).</summary>
    public int BoltsCast { get; private set; }
    public Blizzard LastBlizzard { get; private set; }
    public Updraft LastUpdraft { get; private set; }
    /// <summary>Creatures the last snap burst.</summary>
    public int LastSnapCount { get; private set; }

    public float AlimusRegen => Tune.Elementalist.AlimusRegen * Stats.AlimusRegenMult;
    public float UpdraftCost => Tune.Elementalist.UpdraftCost;
    public float BlizzardCost => Tune.Elementalist.BlizzardCost;
    public float SnapCost => Tune.Elementalist.SnapCost;
    /// <summary>For the HUD: the updraft's short recovery and the snap's, 0 = ready.</summary>
    public float UpdraftCooldownFrac => Math.Clamp(_updraftCd / 0.5f, 0, 1);
    public float SnapCooldownFrac => Math.Clamp(_snapCd / Tune.Elementalist.SnapCooldown, 0, 1);
    /// <summary>What a snap would burst right now (for the HUD): the frozen creatures in view, or with Cinder Snap the burning ones.</summary>
    public int SnapTargets => Snappable().Count();

    /// <summary>The Elementalist's bolts are frost (Frostbolt); a copy goes by its game's flags (for the staff's orb).</summary>
    public bool FrostElement => IsRemote ? (_netFlags & HfFrost) != 0 : Stats.Frostbolt;

    /// <summary>Test harness and level changes: set the reserve directly.</summary>
    public void SetAlimus(float value) => Alimus = Math.Clamp(value, 0, Stats.AlimusMax);

    public void GainAlimus(float amount)
    {
        if (!IsElementalist || amount <= 0) return;
        Alimus = Math.Min(Stats.AlimusMax, Alimus + amount);
    }

    private void TickElementalist(float dt)
    {
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        _boltCd -= dt; _updraftCd -= dt; _snapCd -= dt;
        Alimus = Math.Min(Stats.AlimusMax, Alimus + AlimusRegen * dt);
        if (_snapAt >= 0 && (_snapAt -= dt) < 0) BurstSnap();
    }

    /// <summary>Spends alimus for a spell, or says there isn't enough (the press is spent either way).</summary>
    private bool PayAlimus(float cost)
    {
        if (SpendAlimus(cost)) return true;
        SayNo("NOT ENOUGH ALIMUS");
        return false;
    }

    /// <summary>Takes the cost from the alimus held; with Blood Channeling, what's lacking is paid in health (never the last of it).</summary>
    private bool SpendAlimus(float cost)
    {
        if (Alimus >= cost - 0.001f) { Alimus = Math.Max(0, Alimus - cost); return true; }
        if (!Stats.BloodCast) return false;
        float blood = (cost - Alimus) * Tune.Relics.BloodPerAlimus;
        if (Hp <= blood + 1f) return false;
        Alimus = 0;
        float hp0 = Hp;
        Hp -= blood;
        if (Num.Delta(hp0, Hp) > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -22), "-" + Num.Delta(hp0, Hp), new Color(0.85f, 0.15f, 0.2f), 9, 0.7f);
        return true;
    }

    // ---------------------------------------------------------------- bolts

    private bool CastBolt(Vector2 aim, bool held = false)
    {
        if (_boltCd > 0) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        bool frost = Stats.Frostbolt;
        float range = Tune.Elementalist.BoltRange;
        // (a creature near the line of aim draws the bolt to it)
        float cost = frost ? Tune.Elementalist.FrostCost : Tune.Elementalist.FireCost;
        if (!SpendAlimus(cost)) { _boltCd = 0.3f; if (!held) SayNo("NOT ENOUGH ALIMUS"); return true; }
        var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
        _boltCd = (frost ? Tune.Elementalist.FrostEvery : Tune.Elementalist.FireEvery) / Math.Max(0.2f, Stats.AttackSpeed);
        AttacksStarted++;
        BoltsCast++;
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("cast", 3, frost ? 2.2f : 1.6f);
        LastCast = frost ? "frost" : "fire";
        _castGlow = 0.7f;
        float dmg = (frost ? Tune.Elementalist.FrostDamage : Tune.Elementalist.FireDamage) * Stats.DamageMult * Stats.PrimaryDamageMult;
        var bolt = new ElementBolt { Position = CastPoint + dir * 6f, Dir = dir, Frost = frost, Damage = dmg, Range = range, Caster = this };
        G.Spawn(bolt);
        NetSync.HeroVisual(bolt);
        G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
        G.Sfx.Play(frost ? "clink" : "throw", CastPoint, frost ? -12 : -9, 0.1f, frost ? 1.9f : 1.3f);
        return true;
    }

    /// <summary>
    /// A bolt of this hero's met a creature: the blow, and what the element does to it (a fire
    /// may set it alight; frost chills it, and may freeze a regular creature solid).
    /// </summary>
    public void BoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        float dealt = e.Hurt(bolt.Damage, bolt.Dir * (bolt.Frost ? 30f : 60f), at, bolt.Frost ? DamageKind.Frost : DamageKind.Fire);
        if (dealt > 0)
        {
            OnDealtDamage(dealt);
            if (!e.Dead) e.Freeze(Tune.Feel.HitStopBolt);
        }
        if (e.Dead) return;
        if (bolt.Frost)
        {
            e.Chill(Tune.Elementalist.ChillSlow, Tune.Elementalist.ChillSeconds);
            if (G.Chance(Tune.Elementalist.FreezeChance + Stats.FreezeBonus)) e.FreezeSolid(Tune.Elementalist.FreezeSeconds);
        }
        else if (G.Chance(Stats.IgniteChance)) e.Ignite(Tune.Elementalist.IgniteDps * Stats.DamageMult, Tune.Elementalist.IgniteSeconds);
    }

    // ---------------------------------------------------------------- updraft

    private bool TryUpdraft()
    {
        if (!IsElementalist || _updraftCd > 0) return false;
        _updraftCd = 0.5f;
        if (!PayAlimus(UpdraftCost)) return true;
        // it rises from the ground at your feet (or from where you hang in the air, if the ground is far)
        var foot = GlobalPosition + new Vector2(0, 13f);
        if (!IsOnFloor() && G.Cave.FindFloor(GlobalPosition, 70f, out var floor)) foot = floor;
        bool narrow = Stats.NarrowDraft;
        var draft = new Updraft
        {
            Position = foot,
            Width = Tune.Elementalist.UpdraftWidth * (narrow ? 0.5f : 1f),
            Height = Tune.Elementalist.UpdraftHeight + (narrow ? Tune.Elementalist.NarrowExtra : 0f),
            Life = narrow ? Tune.Elementalist.NarrowSeconds : Tune.Elementalist.UpdraftSeconds,
        };
        G.Spawn(draft);
        NetSync.HeroVisual(draft);
        LastUpdraft = draft;
        Anim.Once("hex", 3, 1.5f);
        LastCast = "updraft";
        _castGlow = 1f;
        G.Fx.Shockwave(foot, 34, new Color(0.85f, 0.95f, 1f, 0.3f), 0.4f);
        G.Fx.Dust(foot, 6, 1.4f, new Color(0.85f, 0.9f, 0.95f, 0.35f));
        G.Sfx.Play("dodge", GlobalPosition, -4, 0.05f, 0.6f);
        G.Main.Rumble(0.25f, 0.1f, 0.12f);
        return true;
    }

    /// <summary>
    /// Inside a column of air: gravity is a fraction of itself and so is the speed you fall at, so a
    /// jump goes far higher and a fall is a slow drift. Nothing lifts you. A fall already faster
    /// than the column allows is slowed to it over a moment, not snapped.
    /// </summary>
    private void UpdraftEase(Updraft draft, ref float gravMult, ref float fallCap, ref Vector2 v, float dt)
    {
        float k = draft.Strength;
        gravMult = Mathf.Lerp(1f, Tune.Elementalist.UpdraftGravityMult, k);
        fallCap = MaxFall * Mathf.Lerp(1f, Tune.Elementalist.UpdraftFallMult, k);
        if (v.Y > fallCap)
        {
            v.Y = Mathf.MoveToward(v.Y, fallCap, 2400f * dt);
            fallCap = Math.Max(fallCap, v.Y);
        }
    }

    // ---------------------------------------------------------------- blizzard

    private bool TryBlizzard(Vector2 aim, float aimDist)
    {
        if (!IsElementalist || !AbilityChargeReady) return false;
        if (!PayAlimus(BlizzardCost)) return true;
        SpendAbilityCharge();
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var at = BlizzardSpot(aim, aimDist);
        bool fire = Stats.Firestorm;
        float secondsMult = Stats.BlizzardSecondsMult;
        var storm = new Blizzard
        {
            Position = at,
            Radius = Tune.Elementalist.BlizzardRadius * Stats.BlizzardWideMult,
            Seconds = Tune.Elementalist.BlizzardSeconds * secondsMult,
            Ticks = Mathf.RoundToInt(Tune.Elementalist.BlizzardTicks * secondsMult),
            Fire = fire,
            Damage = (fire ? Tune.Elementalist.FirestormDamage : Tune.Elementalist.BlizzardDamage) * Stats.DamageMult,
            FreezeChance = fire ? 0f : Tune.Elementalist.BlizzardFreeze + Stats.FreezeBonus,
            // (Kindling's extra chance lights a Firestorm too)
            IgniteChance = fire ? Tune.Elementalist.FirestormIgnite + (Stats.IgniteChance - Tune.Elementalist.IgniteChance) : 0f,
            IgniteDps = Tune.Elementalist.IgniteDps * Stats.DamageMult, IgniteSeconds = Tune.Elementalist.IgniteSeconds,
            Caster = this,
        };
        G.Spawn(storm);
        NetSync.HeroVisual(storm);
        LastBlizzard = storm;
        var dir = (at - CastPoint).Normalized();
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("heal", 3, 1.4f);
        LastCast = fire ? "firestorm" : "blizzard";
        _castGlow = 1f;
        G.Fx.Beam(CastPoint, at, fire ? new Color(1f, 0.55f, 0.2f, 0.6f) : new Color(0.8f, 0.94f, 1f, 0.6f));
        G.Sfx.Play(fire ? "lava" : "gasp", at, -4, 0.1f, fire ? 0.8f : 0.5f);
        return true;
    }

    /// <summary>
    /// Where the storm is called down: on the creature nearest your aim within reach; else where
    /// the mouse points (up to the reach) or, on a controller, a good way along your aim. A spot
    /// in the open air settles down onto the ground just below it.
    /// </summary>
    private Vector2 BlizzardSpot(Vector2 aim, float aimDist)
    {
        float reach = Tune.Elementalist.BlizzardRange;
        var target = FindSpellTarget(aim, reach, 30f);
        if (target != null) return target.GlobalPosition;
        float d = aimDist > 1f ? Math.Min(aimDist, reach) : reach * 0.6f;
        // (never through rock: it stops where the way is blocked)
        var from = CastPoint;
        var at = from;
        for (float t = 8f; t <= d; t += 8f)
        {
            var p = from + aim * t;
            if (G.Cave.IsSolid(p)) break;
            at = p;
        }
        if (G.Cave.FindFloor(at, 48f, out var floor)) at = floor - new Vector2(0, Tune.Elementalist.BlizzardRadius * 0.7f);
        return at;
    }

    /// <summary>One strike of this hero's storm on a creature: frost that may freeze it, or fire that may set it alight.</summary>
    public void StormStruck(Blizzard storm, Enemy e)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        var from = (e.GlobalPosition - storm.GlobalPosition).Normalized();
        float dealt = e.Hurt(storm.Damage, from * 10f, e.GlobalPosition - from * e.HitRadius * 0.5f, storm.Fire ? DamageKind.Fire : DamageKind.Frost);
        if (dealt > 0) OnDealtDamage(dealt);
        if (e.Dead) return;
        if (storm.Fire) { if (G.Chance(storm.IgniteChance)) e.Ignite(storm.IgniteDps, storm.IgniteSeconds); }
        else
        {
            e.Chill(Tune.Elementalist.ChillSlow * 0.5f, 0.5f);
            if (G.Chance(storm.FreezeChance)) e.FreezeSolid(Tune.Elementalist.FreezeSeconds);
        }
    }

    // ---------------------------------------------------------------- snap

    /// <summary>What a snap bursts: every frozen creature in view (with Cinder Snap, every burning one).</summary>
    private IEnumerable<Enemy> Snappable()
    {
        var me = GlobalPosition;
        bool cinder = Stats.CinderSnap;
        foreach (var e in G.Enemies)
        {
            if (e.Dead || !e.CanBeHit) continue;
            var d = e.GlobalPosition - me;
            if (Math.Abs(d.X) > Tune.Elementalist.SnapViewX || Math.Abs(d.Y) > Tune.Elementalist.SnapViewY) continue;
            if (cinder ? e.Ignited : e.FrozenSolid) yield return e;
        }
    }

    private bool TrySnap()
    {
        if (!IsElementalist || _snapCd > 0 || _snapAt >= 0) return false;
        var marked = Snappable().ToList();
        if (marked.Count == 0) { _snapCd = 0.3f; SayNo(Stats.CinderSnap ? "NOTHING BURNING" : "NOTHING FROZEN"); return true; }
        _snapCd = Tune.Elementalist.SnapCooldown;
        if (!PayAlimus(SnapCost)) return true;
        _snapping = marked;
        // the fist closes (the clip's clench), and on it everything marked bursts
        _snapAt = 0.12f;
        var dir = (marked[0].GlobalPosition - CastPoint).Normalized();
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("rupture", 3, 2.2f);
        LastCast = Stats.CinderSnap ? "cinder" : "snap";
        _castGlow = 1f;
        foreach (var e in marked) G.Fx.Converge(e.GlobalPosition, e.HitRadius + 14, Stats.CinderSnap ? ElementBolt.FireColor : ElementBolt.FrostColor, 6, 0.12f);
        return true;
    }

    /// <summary>The snap lands: each marked creature bursts, and the burst splashes whatever stands near it.</summary>
    private void BurstSnap()
    {
        _snapAt = -1;
        var marked = _snapping ?? new List<Enemy>();
        _snapping = null;
        bool cinder = Stats.CinderSnap;
        float dmg = (cinder ? Tune.Elementalist.CinderDamage : Tune.Elementalist.SnapDamage) * Stats.DamageMult;
        float splash = (cinder ? Tune.Elementalist.CinderSplash : Tune.Elementalist.SnapSplash) * Stats.DamageMult;
        float radius = Tune.Elementalist.SnapRadius * Stats.SnapWideMult;
        var col = cinder ? ElementBolt.FireColor : ElementBolt.FrostColor;
        int burst = 0;
        foreach (var e in marked)
        {
            if (!IsInstanceValid(e) || e.Dead || !e.CanBeHit) continue;
            var at = e.GlobalPosition;
            burst++;
            // the ice (or the fire) goes, in a burst of shards (or cinders)
            if (cinder) e.Quench(); else e.Thaw();
            float dealt = e.Hurt(dmg, Vector2.Up * 60f, at, cinder ? DamageKind.Fire : DamageKind.Frost);
            if (dealt > 0) OnDealtDamage(dealt);
            G.Fx.Flash(at, e.HitRadius + 14, col, 0.14f);
            G.Fx.Burst(at, cinder ? new Color(1f, 0.6f, 0.2f) : new Color(0.85f, 0.97f, 1f), 18, 190, 2.4f, 0.45f, cinder ? -60f : 260f);
            G.Fx.Ring(at, radius, new Color(col, 0.8f), 0.3f);
            G.Sfx.Play(cinder ? "lava" : "rock", at, -4, 0.1f, cinder ? 1.2f : 2f);
            foreach (var o in G.Enemies.ToArray())
            {
                if (o == e || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > radius + o.HitRadius) continue;
                if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
                float d2 = o.Hurt(splash, (o.GlobalPosition - at).Normalized() * 80f, o.GlobalPosition, cinder ? DamageKind.Fire : DamageKind.Frost);
                if (d2 > 0) OnDealtDamage(d2);
            }
        }
        LastSnapCount = burst;
        if (burst > 0)
        {
            G.Fx.AddShake(2f + burst);
            G.Main.Rumble(0.4f, 0.4f, 0.15f);
            if (Stats.SnapEcho) GainAlimus(Tune.Elementalist.EchoAlimus * burst);
        }
    }
}
