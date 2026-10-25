using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Vitalist (nature, wind, water; <see cref="Tune.ElementalVital"/>). Her drain (the attack), the hex about her (the dodge
/// button) and her heal (the ability) each take the mode's version, or a pair's (<see cref="Elements.DualName"/>):
///  * the drain: Thorn Drain, Wisp Drain, Tide Drain; Bramble Lash, Whirling Siphon, Riptide, Pollen Drain, Life Spring, Rain Siphon;
///  * the hex: Overgrowth, Withering Gale, Drowning Hex; Strangling Grove, Eye of the Tempest, Maelstrom, Spore Storm, Mire Bloom, Squall;
///  * the heal: Regrowth, Breath of Life, Cleansing Wave; Bloom of Life, Second Wind, Wellspring, Seeds on the Wind, Rejuvenation,
///    Mist of Mending.
/// Nature roots and bleeds (entangled), wind reaches and moves, water soaks and mends. Her Rupture is as it was.
/// </summary>
public partial class Player
{
    /// <summary>The version of the drain under way (for its strike), and its elements.</summary>
    private string _vDrain = "";
    private HeroElement _vDrainA, _vDrainB;
    /// <summary>The drain's and the hex's reach for the cast being made (a wind version's farther), x their own.</summary>
    private float _drainReach = 1f, _hexReach = 1f;
    /// <summary>The heal version about to be cast (read as the heal begins), and whether it's free of the ability's cooldown (testing).</summary>
    private string _vHealNext = "";
    private bool _vHealFree;

    /// <summary>What a button would be now, without spending anything.</summary>
    private string NextVersion(ElemSlot slot) => DualReady ? DualNameFor(slot) : Elements.SingleName(Stats.Hero, slot, Mode);

    private static bool WindDrain(string name) => name is "Wisp Drain" or "Whirling Siphon" or "Pollen Drain" or "Rain Siphon";

    // ================================================================== the drain

    /// <summary>The attack: the mode's drain (a wind one reaching farther). A cast at nothing is a plain wisp and spends nothing.</summary>
    private bool VitalDrain(Vector2 aim, bool held)
    {
        if (_drainCd > 0) return false;
        string next = NextVersion(ElemSlot.Attack);
        bool wind = WindDrain(next);
        var dir = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        _drainReach = wind ? Tune.ElementalVital.WispReach : 1f;
        try
        {
            if (FindSpellTarget(dir, DrainRange) == null) { _vDrain = ""; return CastDrain(aim, held); }
            var (name, a, b) = SpendFor(ElemSlot.Attack);
            _vDrain = name; _vDrainA = a; _vDrainB = b;
            if (!CastDrain(aim, held)) { _vDrain = ""; return false; }
            if (wind) { _drainCd *= Tune.ElementalVital.WispPace; _drainDmg *= Tune.ElementalVital.WispDamage; }
            _lastEl = a;
            Used(name, b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b));
            return true;
        }
        finally { _drainReach = 1f; }
    }

    /// <summary>Creatures within <paramref name="r"/> of another, in its sight, nearest first.</summary>
    private static List<Enemy> NearFoe(Enemy t, float r) => G.Enemies
        .Where(e => e != t && !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(t.GlobalPosition) < r + e.HitRadius && InSight(t.GlobalPosition, e))
        .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(t.GlobalPosition)).ToList();

    /// <summary>The drain's strike landed on <paramref name="t"/>: what the version adds (<paramref name="dealt"/>: what it tore out).</summary>
    private void VitalDrainStruck(Enemy t, float dmg, float dealt)
    {
        if (_vDrain == SimpleVer) { SimpleTouch(t, KitRole.Primary, CastPoint); return; }
        float p = Stats.DamageMult, full = Tune.Elements.Full;
        var nature = Elements.ColorOf(HeroElement.Nature);
        var water = Elements.ColorOf(HeroElement.Water);
        var wind = Elements.ColorOf(HeroElement.Wind);
        var at = t.GlobalPosition;
        switch (_vDrain)
        {
            case "Thorn Drain":
                // thorns burst out of it as its life is torn away: nature, and more of its life comes home
                t.Build(HeroElement.Nature, Tune.ElementalVital.ThornBuild, p, this);
                GainVitalForce(dealt * Stats.VitalForceGain * Tune.ElementalVital.ThornForce);
                Thorns(at, nature, 6);
                break;
            case "Wisp Drain":
                // a quick thread of air carries it from farther off
                G.Fx.Directional(at, (CastPoint - at).Normalized(), 0.2f, new Color(wind, 0.8f), 8, 260, 1.4f, 0.3f, 0, 1);
                break;
            case "Tide Drain":
                // it comes home as water, washing over her
                t.Build(HeroElement.Water, Tune.ElementalVital.TideBuild, p, this);
                Heal(dealt * Tune.ElementalVital.TideHeal);
                G.Fx.Splash(at, 0.5f, new Color(water, 0.9f));
                break;
            case "Bramble Lash":
                // a full meter of nature, and brambles lash out from it to the creatures beside it, draining them too
                t.Build(HeroElement.Nature, full, p, this);
                Thorns(at, nature, 10);
                foreach (var o in NearFoe(t, Tune.ElementalVital.LashRadius).Take(Tune.ElementalVital.LashCount))
                {
                    G.Fx.Beam(at, o.GlobalPosition, new Color(nature, 0.85f));
                    DrainFrom(o, dmg * Tune.ElementalVital.LashShare, 0.7f);
                    if (!o.Dead) o.Build(HeroElement.Nature, Tune.ElementalVital.ThornBuild, p, this);
                }
                break;
            case "Whirling Siphon":
                // the wind takes more of them at once: the next nearest in reach are drained as well
                foreach (var o in G.Enemies.Where(e => e != t && !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(CastPoint) < DrainRange * Tune.ElementalVital.WispReach + e.HitRadius && InSight(CastPoint, e))
                    .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(CastPoint)).Take(Tune.ElementalVital.SiphonCount).ToList())
                {
                    G.Fx.Beam(CastPoint, o.GlobalPosition, new Color(wind, 0.8f));
                    DrainFrom(o, dmg * Tune.ElementalVital.SiphonShare, 0.7f);
                }
                for (int k = 0; k < 8; k++) G.Fx.Directional(CastPoint + Vector2.Right.Rotated(k * Mathf.Tau / 8f) * 14f, Vector2.Right.Rotated(k * Mathf.Tau / 8f + 1.6f), 0.2f, new Color(wind, 0.7f), 1, 180, 1.5f, 0.3f, 0, 1);
                break;
            case "Riptide":
                // a full meter of water, and the current hauls it toward her
                t.Build(HeroElement.Water, full, p, this);
                t.Shove((CastPoint - at).Normalized() * Tune.ElementalVital.RiptidePull + new Vector2(0, -40f));
                G.Fx.Directional(at, (CastPoint - at).Normalized(), 0.3f, new Color(water, 0.85f), 14, 240, 2f, 0.4f, 0, 0);
                break;
            case "Pollen Drain":
            case "Rain Siphon":
            {
                // pollen (nature) or rain (water) carried off it on the wind, to everything about it
                var el = _vDrain == "Pollen Drain" ? HeroElement.Nature : HeroElement.Water;
                var col = Elements.ColorOf(el);
                t.Build(el, Tune.ElementalVital.CloudBuild, p, this);
                foreach (var o in NearFoe(t, Tune.ElementalVital.CloudRadius)) o.Build(el, Tune.ElementalVital.CloudBuild, p, this);
                for (int k = 0; k < 16; k++)
                {
                    var o2 = at + G.RandDir() * G.Range(0f, Tune.ElementalVital.CloudRadius);
                    if (el == HeroElement.Water) G.Fx.Burst(o2 + new Vector2(0, -30), new Color(col, 0.8f), 1, 40, 1.4f, 0.5f, 400);
                    else G.Fx.Ember(o2, new Color(1f, 0.95f, 0.5f));
                }
                G.Fx.Ring(at, Tune.ElementalVital.CloudRadius, new Color(col, 0.6f), 0.4f, FxLayer.RingStyle.Wave);
                break;
            }
            case "Life Spring":
                // what it tears out wells up in her as a spring: every friend near her is mended by part of it
                t.Build(HeroElement.Nature, Tune.ElementalVital.SpringBuild, p, this);
                foreach (var h in HeroesNear(Tune.Vitalist.HealRange))
                {
                    h.Heal(dealt * Tune.ElementalVital.SpringHeal);
                    G.Fx.Ring(h.GlobalPosition + new Vector2(0, -6), 14, new Color(water, 0.8f), 0.3f, FxLayer.RingStyle.Wave);
                    if (h != this) G.Fx.Beam(CastPoint, h.GlobalPosition + new Vector2(0, -6), new Color(nature.Lerp(water, 0.5f), 0.8f));
                }
                break;
        }
    }

    /// <summary>Thorns bursting out of a creature (the nature drains).</summary>
    private static void Thorns(Vector2 at, Color col, int n)
    {
        for (int k = 0; k < n; k++)
        {
            var d = Vector2.Up.Rotated(G.Range(-1.4f, 1.4f));
            G.Fx.Directional(at + d * 4f, d, 0.08f, new Color(col.Darkened(0.2f), 0.95f), 1, G.Range(120, 220), 1.8f, 0.25f, 0, 0);
        }
    }

    // ================================================================== the hex

    /// <summary>The dodge button: the hex about her, in the mode's version (a wind one wider).</summary>
    private bool VitalHex()
    {
        if (!IsVitalist || _hexCd > 0) return false;
        var (name, a, b) = SpendFor(ElemSlot.Move);
        _hexReach = name switch
        {
            "Withering Gale" => Tune.ElementalVital.GaleReach, "Strangling Grove" => Tune.ElementalVital.GroveReach, "Spore Storm" => Tune.ElementalVital.SporeReach,
            "Eye of the Tempest" => Tune.ElementalVital.TempestReach, _ => 1f,
        };
        float r = Tune.Vitalist.HexRadius * Stats.HexRadiusMult * _hexReach;
        try { TryHex(); }
        finally { _hexReach = 1f; }
        if (Tune.Testing.NoCooldowns) _hexCd = Math.Min(_hexCd, 0.25f);
        VitalHexStruck(name, r);
        _lastEl = a;
        Used(name, b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b));
        return true;
    }

    /// <summary>The hex went out to <paramref name="r"/>: what the version does to all in it (vines, wind, water).</summary>
    private void VitalHexStruck(string name, float r)
    {
        float p = Stats.DamageMult, full = Tune.Elements.Full;
        var c = GlobalPosition + new Vector2(0, -6);
        var feet = GlobalPosition + new Vector2(0, 12);
        var foes = G.Enemies.Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(c) <= r + e.HitRadius).ToList();
        Vector2 Away(Enemy e) => (e.GlobalPosition - c).LengthSquared() > 1 ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
        var nature = Elements.ColorOf(HeroElement.Nature);
        var water = Elements.ColorOf(HeroElement.Water);
        var wind = Elements.ColorOf(HeroElement.Wind);
        void Vines(int n) { for (int k = 0; k < n; k++) Thorns(feet + new Vector2(G.Range(-r, r), 0), nature, 1); }
        void Gust(float reach) { for (int k = 0; k < 12; k++) { float a = k * Mathf.Tau / 12f; G.Fx.Directional(c + Vector2.Right.Rotated(a) * 12f, Vector2.Right.Rotated(a), 0.15f, new Color(wind, 0.75f), 1, reach * 2.4f, 1.6f, 0.35f, 0, 1); } }
        void Wave() { G.Fx.Shockwave(feet, r, new Color(water, 0.85f), 0.5f); G.Fx.Splash(feet, 0.7f, new Color(water, 0.9f)); }
        switch (name)
        {
            case "Overgrowth":
                foreach (var e in foes) e.Build(HeroElement.Nature, Tune.ElementalVital.OvergrowthBuild, p, this);
                Vines(14);
                break;
            case "Withering Gale":
                foreach (var e in foes) e.Shove(Away(e) * Tune.ElementalVital.HexShove + new Vector2(0, -60f));
                Gust(r);
                break;
            case "Drowning Hex":
                foreach (var e in foes) e.Build(HeroElement.Water, Tune.ElementalVital.DrownBuild, p, this);
                Wave();
                break;
            case "Strangling Grove":
                foreach (var e in foes) e.Build(HeroElement.Nature, full, p, this);
                Vines(26);
                G.Fx.Shockwave(feet, r, new Color(nature, 0.9f), 0.5f);
                break;
            case "Eye of the Tempest":
                // everything about her is hauled in toward her, where her friends can get at it
                foreach (var e in foes) if (e.GlobalPosition.DistanceTo(c) > 30f) e.Shove(-Away(e) * Tune.ElementalVital.TempestPull + new Vector2(0, -50f));
                for (int k = 0; k < 16; k++) { float a = k * Mathf.Tau / 16f; G.Fx.Directional(c + Vector2.Right.Rotated(a) * r * 0.9f, -Vector2.Right.Rotated(a - 0.4f), 0.15f, new Color(wind, 0.75f), 1, r * 2f, 1.6f, 0.4f, 0, 1); }
                break;
            case "Maelstrom":
            {
                // a full meter of water to all about her, and a pool of healing water at her feet
                foreach (var e in foes) e.Build(HeroElement.Water, full, p, this);
                Wave();
                var pool = new HealingPool { Position = feet, Radius = Tune.ElementalVital.PoolRadius, Rate = Tune.ElementalVital.PoolRate * Stats.HealMult, Life = Tune.ElementalVital.PoolSeconds };
                G.Spawn(pool);
                NetSync.HeroVisual(pool);
                break;
            }
            case "Spore Storm":
                foreach (var e in foes) { e.Build(HeroElement.Nature, Tune.ElementalVital.MixBuild, p, this); e.Shove(Away(e) * Tune.ElementalVital.HexShove * 0.75f + new Vector2(0, -50f)); }
                for (int k = 0; k < 20; k++) G.Fx.Ember(c + G.RandDir() * G.Range(10, r), new Color(1f, 0.95f, 0.5f));
                Gust(r);
                break;
            case "Mire Bloom":
                foreach (var e in foes) { e.Mire(Tune.ElementalVital.MireSeconds); e.Build(HeroElement.Nature, Tune.ElementalVital.MixBuild, p, this); }
                G.Fx.Shockwave(feet, r, new Color(nature.Lerp(water, 0.5f).Darkened(0.3f), 0.9f), 0.6f);
                Vines(10);
                break;
            case "Squall":
                foreach (var e in foes) { e.Build(HeroElement.Water, Tune.ElementalVital.MixBuild, p, this); e.Shove(Away(e) * Tune.ElementalVital.HexShove * 0.85f + new Vector2(0, -50f)); }
                Wave();
                Gust(r);
                break;
        }
        HexesStruck += foes.Count;
    }
    /// <summary>For the tests: creatures the elemental hexes have reached.</summary>
    public int HexesStruck { get; private set; }

    // ================================================================== the heal

    /// <summary>The ability: the mode's heal. It goes out even with no one hurt (for what else the version does), costing its vital force.</summary>
    private bool VitalHeal()
    {
        if (!IsVitalist || _healCd > 0) return false;
        bool free = Tune.Testing.NoCooldowns;
        if (!AbilityChargeReady && !free) return false;
        if (VitalForce < HealCost - 0.001f) return Refuse("NOT ENOUGH VITAL FORCE");
        var (name, a, b) = SpendFor(ElemSlot.Aid);
        _vHealNext = name; _vHealFree = free;
        if (!TryHeal()) { _vHealNext = ""; return false; }
        _lastEl = a;
        Used(name, b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b));
        return true;
    }

    /// <summary>A heal version's reach, x the heal's own.</summary>
    private static float HealReach(string ver) => ver switch
    {
        "Breath of Life" or "Seeds on the Wind" => Tune.ElementalVital.BreathReach, "Second Wind" => 100f, _ => 1f,
    };

    /// <summary>A heal version's strength, x the heal's own.</summary>
    private static float HealAmp(string ver) => ver switch { "Bloom of Life" => Tune.ElementalVital.BloomAmp, "Wellspring" => Tune.ElementalVital.WellAmp, _ => 1f };

    /// <summary>A heal version that gives part of it now and more over time: (now, later) shares of a friend's share; (1, 0) for the rest.</summary>
    private static (float now, float later) HealSpread(string ver) => ver switch
    {
        "Regrowth" => (Tune.ElementalVital.RegrowNow, Tune.ElementalVital.RegrowLater),
        "Rejuvenation" => (Tune.ElementalVital.RejuvNow, Tune.ElementalVital.RejuvLater),
        _ => (1f, 0f),
    };

    /// <summary>The heal went out (<paramref name="range"/> its reach): what else the version does.</summary>
    private void VitalHealStruck(string ver, float range)
    {
        if (ver == SimpleVer) return;
        var near = HeroesNear(range);
        var water = Elements.ColorOf(HeroElement.Water);
        var nature = Elements.ColorOf(HeroElement.Nature);
        var feet = GlobalPosition + new Vector2(0, 12);
        switch (ver)
        {
            case "Breath of Life":
            case "Second Wind":
                foreach (var h in near) h.GiveBuff(BuffTailwind, Tune.ElementalVital.TailwindSeconds * (ver == "Second Wind" ? 1.4f : 1f));
                break;
            case "Cleansing Wave":
            case "Rejuvenation":
                foreach (var h in near) h.Cleanse();
                G.Fx.Shockwave(feet, Math.Min(range, 200f), new Color(water, 0.8f), 0.5f);
                break;
            case "Bloom of Life":
            {
                var pool = new HealingPool { Position = feet, Radius = Tune.ElementalVital.PoolRadius, Rate = Tune.ElementalVital.PoolRate * Stats.HealMult, Life = Tune.ElementalVital.PoolSeconds };
                G.Spawn(pool);
                NetSync.HeroVisual(pool);
                for (int k = 0; k < 16; k++) G.Fx.Ember(feet + new Vector2(G.Range(-60, 60), G.Range(-10, 0)), new Color(1f, 0.7f, 0.85f));
                break;
            }
            case "Wellspring":
                foreach (var h in near) { h.GiveBarrier(Tune.ElementalVital.WellBarrier * h.Stats.MaxHp, Tune.ElementalVital.WellSeconds); h.Cleanse(); }
                G.Fx.Splash(feet, 1f, new Color(water, 0.9f));
                break;
            case "Seeds on the Wind":
                foreach (var h in near) h.GiveMending(Tune.ElementalVital.SeedsMend * Stats.HealMult, Tune.ElementalVital.MendSeconds, 0f);
                for (int k = 0; k < 14; k++) G.Fx.Ember(GlobalPosition + G.RandDir() * G.Range(10, 80), nature);
                break;
            case "Mist of Mending":
            {
                var mist = new SmokeCloud { Position = GlobalPosition + new Vector2(0, -6), Radius = Tune.ElementalVital.MistRadius, Life = Tune.ElementalVital.MistSeconds };
                G.Spawn(mist);
                NetSync.HeroVisual(mist);
                break;
            }
        }
    }
}
