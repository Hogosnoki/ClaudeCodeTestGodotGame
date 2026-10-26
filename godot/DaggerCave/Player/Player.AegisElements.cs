using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Aegis (wind, earth, frost; <see cref="Tune.ElementalAegis"/>). Her bubble shot (the attack: the ward bolt), her bubble field
/// (the dodge button) and her barrier (the ability) each take the mode's version, or a pair's (<see cref="Elements.DualName"/>):
///  * the shot: Gale Bubble, Stone Bubble, Rime Bubble; Boulder Bubble, Frost Orb, Bubble Volley, Hail Bubble, Sand Bubble, Snow Bubble;
///  * the field: Air Dome, Stone Dome, Ice Dome; Fortress, Glacier Dome, Gale Dome, Crystal Dome, Dust Dome, Blizzard Dome;
///  * the barrier: Wind Ward, Stone Skin, Frost Ward; Bulwark Ward, Glacial Ward, Twin Gale, Permafrost Ward, Sandstorm Ward, Winter Ward.
/// Wind keeps creatures off and speeds friends, earth makes her wards heavier and her shots hit harder, frost fills what comes near with
/// cold. Her Shared Burden (and Smite) is as it was.
/// </summary>
public partial class Player
{
    /// <summary>The barrier and the field versions about to be cast (read as each begins), and whether the barrier is free of the ability's
    /// cooldown (testing).</summary>
    private string _aBarrierNext = "", _aDomeNext = "";
    private bool _aBarrierFree;

    private static Color PairColor(HeroElement a, HeroElement b) => b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);

    // ================================================================== the bubble shot

    /// <summary>The attack: a bubble shot in the mode's version (its speed, weight, how many, what its burst does).</summary>
    private bool AegisShot(Vector2 aim, bool held)
    {
        if (_wardCd > 0) return false;
        var (name, a, b) = SpendFor(ElemSlot.Attack);
        float speed = 1f, dmg = 1f, burst = 1f, pace = 1f;
        int count = 1;
        switch (name)
        {
            case "Gale Bubble": speed = Tune.ElementalAegis.GaleSpeed; pace = Tune.ElementalAegis.GalePace; break;
            case "Stone Bubble": speed = Tune.ElementalAegis.StoneSpeed; dmg = Tune.ElementalAegis.StoneDamage; burst = Tune.ElementalAegis.StoneBurst; pace = Tune.ElementalAegis.StonePace; break;
            case "Boulder Bubble": speed = Tune.ElementalAegis.StoneSpeed; dmg = Tune.ElementalAegis.BoulderDamage; burst = Tune.ElementalAegis.BoulderBurst; pace = Tune.ElementalAegis.StonePace; break;
            case "Bubble Volley": count = Tune.ElementalAegis.VolleyCount; speed = Tune.ElementalAegis.GaleSpeed; break;
            case "Hail Bubble": dmg = Tune.ElementalAegis.HailDamage; break;
            case "Snow Bubble": burst = Tune.ElementalAegis.SnowBurst; break;
        }
        CastWardBolt(aim, held, name, a, speed, dmg, burst, count);
        _wardCd *= pace;
        _lastEl = a;
        Used(name, PairColor(a, b));
        return true;
    }

    /// <summary>An elemental bubble shot burst on <paramref name="e"/>: what its version does to everything its burst reached.</summary>
    private void AegisShotBurst(ElementBolt bolt, Enemy e, Vector2 at, List<Enemy> struck)
    {
        if (bolt.Version == SimpleVer) { foreach (var t in struck) SimpleTouch(t, KitRole.Primary, at); return; }
        float p = Stats.DamageMult, full = Tune.Elements.Full, build = Tune.ElementalAegis.ShotBuild;
        Vector2 Away(Enemy t) => (t.GlobalPosition - at).LengthSquared() > 1 ? (t.GlobalPosition - at).Normalized() : Vector2.Up;
        var col = bolt.Tint;
        float reach = Tune.Aegis.BurstRadius * Stats.BurstMult * bolt.BurstMult;
        foreach (var t in struck)
        {
            if (t.Dead) continue;
            switch (bolt.Version)
            {
                case "Gale Bubble": t.Shove(Away(t) * Tune.ElementalAegis.BurstShove + new Vector2(0, -60f)); break;
                case "Stone Bubble": t.Build(HeroElement.Earth, build, p, this); break;
                case "Rime Bubble": t.Build(HeroElement.Frost, build, p, this); break;
                case "Boulder Bubble": t.Build(HeroElement.Earth, t == e ? full : build, p, this); break;
                case "Frost Orb": t.Build(HeroElement.Frost, full, p, this); break;
                case "Hail Bubble":
                    if (!t.Shatter(p, this)) { t.Build(HeroElement.Earth, build, p, this); if (!t.Dead) t.Build(HeroElement.Frost, build, p, this); }
                    break;
                case "Sand Bubble": t.Sap(Tune.ElementalAegis.SandSap); break;
                case "Snow Bubble": t.Build(HeroElement.Frost, build, p, this); if (!t.Dead) t.Shove(Away(t) * Tune.ElementalAegis.BurstShove * 0.75f + new Vector2(0, -40f)); break;
            }
        }
        // (how it looks as it bursts: a gust, a crack of stone, a puff of frost, a cloud of grit)
        switch (bolt.Version)
        {
            case "Gale Bubble":
            case "Snow Bubble":
                for (int k = 0; k < 10; k++) { float a = k * Mathf.Tau / 10f; G.Fx.Directional(at, Vector2.Right.Rotated(a), 0.15f, new Color(col, 0.8f), 1, reach * 3f, 1.6f, 0.3f, 0, 1); }
                if (bolt.Version == "Snow Bubble") G.Fx.Burst(at, new Color(0.92f, 0.98f, 1f), 14, 140, 2f, 0.5f, 60);
                break;
            case "Stone Bubble":
            case "Boulder Bubble":
            case "Hail Bubble":
                G.Fx.Debris(at, Elements.ColorOf(HeroElement.Earth), bolt.Version == "Boulder Bubble" ? 16 : 8, 220);
                G.Fx.AddShake(bolt.Version == "Boulder Bubble" ? 3f : 1.5f);
                G.Sfx.Play("rock", at, -4, 0.05f, 0.9f);
                if (bolt.Version == "Hail Bubble") G.Fx.Burst(at, new Color(0.9f, 0.97f, 1f), 12, 180, 2f, 0.4f, 160);
                break;
            case "Rime Bubble":
            case "Frost Orb":
                G.Fx.Burst(at, new Color(0.88f, 0.97f, 1f), bolt.Version == "Frost Orb" ? 22 : 12, 160, 2.2f, 0.5f, 40);
                G.Sfx.Play("clink", at, -4, 0.05f, 0.8f);
                break;
            case "Sand Bubble":
            {
                var cloud = new SmokeCloud { Position = at, Radius = Tune.ElementalAegis.DustRadius, Life = Tune.ElementalAegis.DustSeconds };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                G.Fx.Dust(at, 10, 2.4f, new Color(0.7f, 0.6f, 0.45f, 0.8f));
                break;
            }
        }
        G.Fx.Ring(at, reach, new Color(col, 0.8f), 0.3f, FxLayer.RingStyle.Burst);
    }

    // ================================================================== the barrier

    /// <summary>The ability: the barrier in the mode's version.</summary>
    private bool AegisBarrier(Vector2 aim)
    {
        if (!IsAegis) return false;
        bool free = Tune.Testing.NoCooldowns;
        if (!AbilityChargeReady && !free) return false;
        var (name, a, b) = SpendFor(ElemSlot.Aid);
        _aBarrierNext = name; _aBarrierFree = free;
        if (!TryBarrier(aim)) { _aBarrierNext = ""; return false; }
        _lastEl = a;
        Used(name, PairColor(a, b));
        return true;
    }

    /// <summary>A barrier version's strength, x the barrier's own.</summary>
    private static float WardAmp(string ver) => ver switch
    {
        "Stone Skin" => Tune.ElementalAegis.StoneWard, "Bulwark Ward" => Tune.ElementalAegis.BulwarkWard, "Glacial Ward" => Tune.ElementalAegis.GlacialWard,
        "Permafrost Ward" => Tune.ElementalAegis.PermafrostWard, _ => 1f,
    };

    /// <summary>A barrier version went on <paramref name="who"/>: what else it gives them.</summary>
    private void AegisWardGiven(string ver, Player who)
    {
        // (the simple kit's barrier lends the hero it wraps her element)
        if (ver == SimpleVer) { who.GiveBuff(BuffImbue(Mode), Tune.Elements.ImbueSeconds); return; }
        float secs = Tune.Aegis.BarrierSeconds;
        switch (ver)
        {
            case "Wind Ward":
            case "Twin Gale":
                who.GiveBuff(BuffTailwind, Tune.ElementalAegis.TailwindSeconds * (ver == "Twin Gale" ? 1.4f : 1f));
                break;
            case "Frost Ward":
            case "Permafrost Ward":
                who.GiveBuff(BuffFrostArmor, secs);
                break;
            case "Glacial Ward":
                who.GiveBuff(BuffFrostWard, Tune.ElementalAegis.FrostWardSeconds);
                break;
            case "Winter Ward":
                who.GiveBuff(BuffFrostArmor, secs);
                who.GiveBuff(BuffTailwind, Tune.ElementalAegis.TailwindSeconds);
                break;
            case "Sandstorm Ward":
            {
                var cloud = new SmokeCloud { Position = who.GlobalPosition + new Vector2(0, -6), Radius = Tune.ElementalAegis.DustRadius + 6f, Life = Tune.ElementalAegis.DustSeconds + 1f };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                break;
            }
        }
        if (ver is "Stone Skin" or "Bulwark Ward" or "Permafrost Ward") G.Fx.Debris(who.GlobalPosition + new Vector2(0, -6), Elements.ColorOf(HeroElement.Earth), 6, 100);
    }

    // ================================================================== the bubble field

    /// <summary>The dodge button: the bubble field in the mode's version.</summary>
    private bool AegisDome()
    {
        if (!IsAegis || _bubbleCd > 0) return false;
        var (name, a, b) = SpendFor(ElemSlot.Move);
        _aDomeNext = name;
        TryBubble();
        _aDomeNext = "";
        if (Tune.Testing.NoCooldowns) _bubbleCd = Math.Min(_bubbleCd, 0.3f);
        _lastEl = a;
        Used(name, PairColor(a, b));
        return true;
    }

    private static float DomeReach(string ver) => ver == "Gale Dome" ? Tune.ElementalAegis.GaleReach : 1f;
    private static float DomeCap(string ver) => ver switch
    {
        "Stone Dome" => Tune.ElementalAegis.StoneCap, "Fortress" => Tune.ElementalAegis.FortressCap, "Crystal Dome" => Tune.ElementalAegis.CrystalCap, _ => 1f,
    };
    private static float DomeTime(string ver) => ver == "Fortress" ? Tune.ElementalAegis.FortressTime : 1f;

    /// <summary>The field went up out to <paramref name="r"/>: what the version does to the creatures in it (and the heroes it wrapped).</summary>
    private void AegisDomeUp(string ver, float r, List<Player> wrapped)
    {
        float p = Stats.DamageMult, full = Tune.Elements.Full;
        var c = GlobalPosition + new Vector2(0, -6);
        var foes = G.Enemies.Where(e => !e.Dead && e.CanBeHit && e.GlobalPosition.DistanceTo(c) <= r + e.HitRadius).ToList();
        if (ver == SimpleVer)
        {
            foreach (var e in foes) SimpleTouch(e, KitRole.Special, c);
            foreach (var h in wrapped) h.GiveBuff(BuffImbue(Mode), Tune.Elements.ImbueSeconds);
            ElementWorld.Touch(Mode, c, r);
            G.Fx.Ring(c, r, new Color(Elements.ColorOf(Mode), 0.75f), 0.4f, FxLayer.RingStyle.Burst);
            return;
        }
        Vector2 Away(Enemy e) => (e.GlobalPosition - c).LengthSquared() > 1 ? (e.GlobalPosition - c).Normalized() : Vector2.Up;
        var wind = Elements.ColorOf(HeroElement.Wind);
        void Gust() { for (int k = 0; k < 14; k++) { float a = k * Mathf.Tau / 14f; G.Fx.Directional(c + Vector2.Right.Rotated(a) * 16f, Vector2.Right.Rotated(a), 0.12f, new Color(wind, 0.75f), 1, r * 2.2f, 1.6f, 0.35f, 0, 1); } }
        switch (ver)
        {
            case "Air Dome":
            case "Gale Dome":
                foreach (var e in foes) e.Shove(Away(e) * Tune.ElementalAegis.DomeShove + new Vector2(0, -60f));
                Gust();
                break;
            case "Ice Dome":
                foreach (var e in foes) e.Build(HeroElement.Frost, Tune.ElementalAegis.DomeBuild, p, this);
                G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), r, new Color(Elements.ColorOf(HeroElement.Frost), 0.85f), 0.5f);
                break;
            case "Glacier Dome":
                foreach (var e in foes) e.Build(HeroElement.Frost, full, p, this);
                G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), r, new Color(0.9f, 0.97f, 1f, 0.9f), 0.5f);
                G.Fx.Burst(c, new Color(0.9f, 0.97f, 1f), 24, 200, 2.2f, 0.5f, 40);
                break;
            case "Blizzard Dome":
                foreach (var e in foes) { e.Build(HeroElement.Frost, Tune.ElementalAegis.DomeBuild, p, this); if (!e.Dead) e.Shove(Away(e) * Tune.ElementalAegis.DomeShove * 0.8f + new Vector2(0, -50f)); }
                Gust();
                G.Fx.Burst(c, new Color(0.92f, 0.98f, 1f), 20, 180, 2f, 0.5f, 60);
                break;
            case "Dust Dome":
            {
                foreach (var e in foes) e.Sap(Tune.ElementalAegis.SandSap);
                var cloud = new SmokeCloud { Position = c, Radius = Math.Min(r * 0.6f, 90f), Life = Tune.ElementalAegis.DustSeconds + 2f };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                break;
            }
            case "Crystal Dome":
                foreach (var h in wrapped) h.GiveBuff(BuffFrostArmor, Tune.Aegis.BubbleSeconds * 0.5f);
                G.Fx.Burst(c, new Color(0.85f, 0.95f, 1f), 16, 120, 2f, 0.5f, 0);
                break;
            case "Stone Dome":
            case "Fortress":
                G.Fx.Debris(GlobalPosition + new Vector2(0, 10), Elements.ColorOf(HeroElement.Earth), ver == "Fortress" ? 16 : 8, 160);
                G.Fx.AddShake(ver == "Fortress" ? 3f : 1.5f);
                break;
        }
        DomeFoesStruck += foes.Count;
    }
    /// <summary>For the tests: creatures the elemental fields have reached.</summary>
    public int DomeFoesStruck { get; private set; }
}
