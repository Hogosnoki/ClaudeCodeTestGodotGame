using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// A creature's elemental build-up (<see cref="Elements"/>, <see cref="Tune.Elements"/>). Elemental blows fill a meter of their element;
/// a full meter takes hold and empties:
///  * fire: set alight; full again while alight, it combusts (a burst on it and those pressed close);
///  * frost: chilled; full again while chilled, frozen solid (a creature that can't be frozen is chilled harder); a heavy blow
///    shatters the frozen (<see cref="Shatter"/>);
///  * earth: stunned (one that can't be is weakened instead);
/// and earth's lighter touch saps a creature (<see cref="Sap"/>): its attacks play slower. Wind pushes (<see cref="Shove"/>). Meters
/// drain a little while after the last that reached them. Online, the host keeps the meters: a copy passes what it is given on.
/// </summary>
public abstract partial class Enemy
{
    private float _fireBuild, _frostBuild, _earthBuild, _natureBuild, _waterBuild, _darkBuild, _buildIdle, _sapT, _mireT, _soakT, _blindT;
    /// <summary>The build-up meters (0..Full), for the meters over a creature and the tests.</summary>
    public float FireBuild => _fireBuild;
    public float FrostBuild => _frostBuild;
    public float EarthBuild => _earthBuild;
    public float NatureBuild => _natureBuild;
    public float WaterBuild => _waterBuild;
    public float DarkBuild => _darkBuild;
    /// <summary>Blinded (a full meter of dark): it has lost every hero, as in smoke.</summary>
    public bool Blinded => _blindT > 0;
    /// <summary>For the tests: times it has been blinded.</summary>
    public int Blindings { get; private set; }
    /// <summary>Soaked (a full meter of water): its fire out, frost building faster in it, fire slower.</summary>
    public bool Soaked => _soakT > 0;
    /// <summary>For the tests: times it has been entangled (a full meter of nature).</summary>
    public int Entangled { get; private set; }
    public bool Sapped => _sapT > 0;
    public float ChillLeft => _chillT;
    /// <summary>For the tests: what has combusted, shattered, and been stunned by earth.</summary>
    public int Combusted { get; private set; }
    public int Shattered { get; private set; }

    /// <summary>How much of a build-up reaches it: all of it for a common creature, less for an elite, least for a guardian or boss.</summary>
    private float BuildTake => IsBoss || IsGuardian ? Tune.Elements.BossTake : Elite ? Tune.Elements.EliteTake : 1f;

    /// <summary>
    /// Adds <paramref name="amount"/> of an element's build-up (<paramref name="power"/>: the hero's damage multiplier, for what a full
    /// meter does; <paramref name="by"/>: the hero, credited with it).
    /// </summary>
    public void Build(HeroElement el, float amount, float power = 1f, Player by = null)
    {
        if (Dead || amount <= 0 || el == HeroElement.None) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Build, (float)el, amount, power); return; }
        amount *= BuildTake;
        // (soaked: frost takes faster, fire slower)
        if (_soakT > 0) amount *= el == HeroElement.Frost ? Tune.Elements.SoakFrost : el == HeroElement.Fire ? Tune.Elements.SoakFire : 1f;
        _buildIdle = 0f;
        float full = Tune.Elements.Full;
        switch (el)
        {
            case HeroElement.Fire:
                _fireBuild += amount;
                if (_fireBuild < full) break;
                _fireBuild = 0;
                if (Ignited) Combust(power, by);
                else Ignite(Tune.Elements.BurnDps * power, Tune.Elements.BurnSeconds);
                break;
            case HeroElement.Frost:
                if (FrozenSolid) break;
                _frostBuild += amount;
                if (_frostBuild < full) break;
                _frostBuild = 0;
                if (Chilled && CanFreezeSolid) { if (!FreezeSolid(Tune.Elements.FreezeSeconds, force: true)) Chill(Tune.Elements.ChillSlow, Tune.Elements.ChillSeconds); }
                else if (Chilled) Chill(Math.Min(0.6f, Tune.Elements.ChillSlow * 1.6f), Tune.Elements.ChillSeconds);
                else
                {
                    Chill(Tune.Elements.ChillSlow, Tune.Elements.ChillSeconds);
                    G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "CHILLED", Elements.ColorOf(HeroElement.Frost), 9, 0.6f);
                }
                break;
            case HeroElement.Earth:
                _earthBuild += amount;
                if (_earthBuild < full) break;
                _earthBuild = 0;
                if (Interruptible) Interrupt(Vector2.Zero, Tune.Elements.StunSeconds, "STUNNED");
                else { Weaken(0.8f, Tune.Elements.StunSeconds * 2f); G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "STAGGERED", Elements.ColorOf(HeroElement.Earth), 9, 0.6f); }
                G.Fx.Ring(GlobalPosition, HitRadius + 8, new Color(Elements.ColorOf(HeroElement.Earth), 0.9f), 0.3f, FxLayer.RingStyle.Burst);
                break;
            case HeroElement.Nature:
                _natureBuild += amount;
                if (_natureBuild < full) break;
                _natureBuild = 0;
                // entangled: vines about it, slowing all it does, and it bleeds sap
                Entangled++;
                Mire(Tune.Elements.EntangleSeconds);
                Bleed(Tune.Elements.EntangleBleed * power, Tune.Elements.EntangleSeconds);
                G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "ENTANGLED", Elements.ColorOf(HeroElement.Nature), 9, 0.7f);
                G.Fx.Ring(GlobalPosition, HitRadius + 8, new Color(Elements.ColorOf(HeroElement.Nature), 0.9f), 0.35f, FxLayer.RingStyle.Wave);
                for (int k = 0; k < 6; k++) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-HitRadius, HitRadius), G.Range(0, HitRadius)), Elements.ColorOf(HeroElement.Nature));
                break;
            case HeroElement.Dark:
                _darkBuild += amount;
                if (_darkBuild < full) break;
                _darkBuild = 0;
                // blinded: it loses every hero for a while, and is laid open to blows
                Blindings++;
                if (_blindT <= 0) G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "BLINDED", Elements.ColorOf(HeroElement.Dark), 9, 0.7f);
                _blindT = Math.Max(_blindT, IsBoss || IsGuardian ? Tune.Elements.BlindSeconds * 0.4f : Tune.Elements.BlindSeconds);
                Expose(Tune.Elements.BlindExpose, Tune.Elements.ExposeSeconds);
                G.Fx.Smoke(GlobalPosition + new Vector2(0, -HitRadius * 0.5f), 4, new Color(0.15f, 0.08f, 0.22f, 0.8f), 30f);
                break;
            case HeroElement.Water:
                _waterBuild += amount;
                if (_waterBuild < full) break;
                _waterBuild = 0;
                // soaked: its fire put out, frost building faster in it
                if (_soakT <= 0) G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "SOAKED", Elements.ColorOf(HeroElement.Water), 9, 0.7f);
                _soakT = Tune.Elements.SoakSeconds;
                if (Ignited) Quench();
                _fireBuild = 0;
                G.Fx.Splash(GlobalPosition, 0.5f, new Color(Elements.ColorOf(HeroElement.Water), 0.9f));
                break;
        }
    }

    /// <summary>Alight and full of fire again: it bursts, the fire's remainder with it.</summary>
    private void Combust(float power, Player by)
    {
        Combusted++;
        float left = _burnT > 0 ? _burnDps * _burnT : 0f;
        Quench();
        float dmg = Tune.Elements.CombustDamage * power + left;
        var at = GlobalPosition;
        G.Fx.Flash(at, HitRadius + 18, new Color(1f, 0.55f, 0.15f), 0.18f);
        G.Fx.Ring(at, Tune.Elements.CombustRadius, new Color(1f, 0.5f, 0.15f, 0.85f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Burst(at, new Color(1f, 0.6f, 0.2f), 18, 160, 2.6f, 0.45f, 80);
        G.Fx.Text(at + new Vector2(0, -HitRadius - 14), "COMBUST", new Color(1f, 0.6f, 0.25f), 11, 0.8f);
        G.Sfx.Play("lava", at, -4, 0.05f, 0.7f);
        G.Fx.AddShake(2f);
        Credit(by, Hurt(dmg, Vector2.Zero, at, DamageKind.Fire));
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == this || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > Tune.Elements.CombustRadius + o.HitRadius) continue;
            if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
            Credit(by, o.Hurt(dmg * Tune.Elements.CombustSplash, (o.GlobalPosition - at).Normalized() * 120f, o.GlobalPosition, DamageKind.Fire));
        }
    }

    /// <summary>Burning, it bursts at once (as if filled with fire again: <see cref="Combust"/>). True if it was alight.</summary>
    public bool BurstAlight(float power, Player by = null)
    {
        if (Dead || !Ignited) return false;
        Combust(power, by);
        return true;
    }

    /// <summary>A heavy blow on a creature frozen solid: the ice bursts, and it with it. True if it was frozen.</summary>
    public bool Shatter(float power, Player by = null)
    {
        if (Dead || !FrozenSolid) return false;
        Shattered++;
        Thaw();
        float dmg = Tune.Elements.ShatterDamage * power;
        var at = GlobalPosition;
        G.Fx.Burst(at, new Color(0.85f, 0.96f, 1f), 22, 200, 2.4f, 0.5f, 220);
        G.Fx.Ring(at, Tune.Elements.ShatterRadius, new Color(0.8f, 0.95f, 1f, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Text(at + new Vector2(0, -HitRadius - 14), "SHATTER", new Color(0.8f, 0.95f, 1f), 11, 0.8f);
        G.Sfx.Play("clink", at, -2, 0.05f, 0.55f);
        G.Fx.AddShake(2.5f);
        Credit(by, Hurt(dmg, Vector2.Zero, at, DamageKind.Frost));
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == this || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > Tune.Elements.ShatterRadius + o.HitRadius) continue;
            Credit(by, o.Hurt(dmg * Tune.Elements.ShatterSplash, (o.GlobalPosition - at).Normalized() * 140f, o.GlobalPosition, DamageKind.Frost));
        }
        return true;
    }

    /// <summary>Earth's lighter touch: for a while its attacks play <see cref="Tune.Elements.SapSlow"/> slower (it doesn't stack).</summary>
    public void Sap(float seconds)
    {
        if (Dead) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Sap, seconds); return; }
        if (_sapT <= 0) G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "SAPPED", Elements.ColorOf(HeroElement.Earth), 8, 0.5f);
        _sapT = Math.Max(_sapT, seconds);
    }

    /// <summary>Wind: a push, felt fully by a light creature and hardly at all by a heavy one.</summary>
    public void Shove(Vector2 push)
    {
        if (Dead || ManualMove || IsBoss) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Shove, 0, v: push); return; }
        float light = Math.Clamp(1f - KnockResist, 0.05f, 1f) * (Elite ? 0.5f : 1f);
        KnockVel = push * light;
        Stun = Math.Max(Stun, 0.25f * light);
    }

    /// <summary>Molten rock about its feet (a Magma Rend): for a while it moves and acts <see cref="Tune.Dual.PoolSlow"/> slower.</summary>
    public void Mire(float seconds)
    {
        if (Dead) return;
        if (Puppet) { NetSync.EffectPuppet(this, NetSync.Effect.Mire, seconds); return; }
        _mireT = Math.Max(_mireT, seconds);
    }
    public bool Mired => _mireT > 0;

    /// <summary>The tempo it plays at while sapped (its attacks) or mired (everything).</summary>
    private float SapTempo => (_sapT > 0 && Attacking ? 1f - Tune.Elements.SapSlow : 1f) * (_mireT > 0 ? 1f - Tune.Dual.PoolSlow : 1f);

    private void TickBuild(float dt)
    {
        if (_sapT > 0) _sapT -= dt;
        if (_mireT > 0) _mireT -= dt;
        if (_blindT > 0)
        {
            _blindT -= dt;
            if (G.Chance(dt * 5f)) G.Fx.Smoke(HeadPoint(4f), 1, new Color(0.12f, 0.06f, 0.18f, 0.7f), 12f);
        }
        if (_soakT > 0)
        {
            _soakT -= dt;
            if (G.Chance(dt * 6f)) G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-HitRadius, HitRadius), -HitRadius * 0.5f), new Color(Elements.ColorOf(HeroElement.Water), 0.8f), 1, 20, 1.4f, 0.4f, 200);
        }
        if (_fireBuild <= 0 && _frostBuild <= 0 && _earthBuild <= 0 && _natureBuild <= 0 && _waterBuild <= 0 && _darkBuild <= 0) return;
        _buildIdle += dt;
        if (_buildIdle < Tune.Elements.DecayDelay) return;
        float d = Tune.Elements.DecayRate * dt;
        _fireBuild = Math.Max(0, _fireBuild - d);
        _frostBuild = Math.Max(0, _frostBuild - d);
        _earthBuild = Math.Max(0, _earthBuild - d);
        _natureBuild = Math.Max(0, _natureBuild - d);
        _waterBuild = Math.Max(0, _waterBuild - d);
        _darkBuild = Math.Max(0, _darkBuild - d);
    }

    /// <summary>A blow from this game's hero while an Elementalist's Kindling is on them: a little fire with it.</summary>
    private void Kindle(Player hero, DamageKind kind)
    {
        if (kind == DamageKind.Fire || Dead || !hero.HasBuff(Player.BuffKindling)) return;
        Build(HeroElement.Fire, Tune.ElementalStaff.KindleBuild, hero.Stats.DamageMult, hero);
    }

    private static void Credit(Player by, float dealt)
    {
        if (dealt > 0 && by != null && GodotObject.IsInstanceValid(by) && !by.IsRemote) by.OnDealtDamage(dealt);
    }
}
