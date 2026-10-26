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
    private float _fireBuild, _frostBuild, _earthBuild, _natureBuild, _waterBuild, _darkBuild, _buildIdle, _sapT, _mireT, _soakT, _blindT, _tangleT;
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
    /// <summary>Entangled now: vines about it, fire building faster in it, frost slower.</summary>
    public bool Tangled => _tangleT > 0;
    /// <summary>For the tests: all of each element that has gone into it (by <see cref="HeroElement"/>), whatever it took hold as.</summary>
    public readonly float[] BuiltTotal = new float[8];
    /// <summary>An element seeping into it (the Vitalist's hex): <see cref="SeepRate"/> a second for <see cref="SeepLeft"/> s more.</summary>
    public HeroElement SeepElement => _seepT > 0 ? _seepEl : HeroElement.None;
    public float SeepLeft => Math.Max(0f, _seepT);
    public float SeepRate => _seepRate;
    private HeroElement _seepEl;
    private float _seepRate, _seepT, _seepTick, _seepPower = 1f;
    private Player _seepBy;
    /// <summary>For the tests: the reactions it has suffered (steam, mud, wildfire caught or spread).</summary>
    public int Steamed { get; private set; }
    public int Mudded { get; private set; }
    public int Fanned { get; private set; }
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
        // (what it refuses by its nature: its own element, the armoured half of earth's stun, the flying the roots)
        float take = StatusTake(el);
        if (take <= 0f) { Refuse(el); return; }
        amount *= take * ReactionMult(el);
        BuiltTotal[(int)el] += amount;
        _buildIdle = 0f;
        float full = Tune.Elements.Full;
        switch (el)
        {
            case HeroElement.Fire:
                _fireBuild += amount;
                if (_fireBuild < full) break;
                _fireBuild = 0;
                // (fire meeting frost: steam)
                if (Chilled || FrozenSolid) { Steam(power, by); break; }
                if (Ignited) Combust(power, by);
                else Ignite(Tune.Elements.BurnDps * power, Tune.Elements.BurnSeconds * (Tangled ? Tune.Elements.TangleBurn : 1f));
                break;
            case HeroElement.Frost:
                if (FrozenSolid) break;
                _frostBuild += amount;
                if (_frostBuild < full) break;
                _frostBuild = 0;
                if (Ignited) { Steam(power, by); break; }
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
                // (earth on the soaked: mud)
                bool mud = Soaked;
                if (mud)
                {
                    Mudded++;
                    _soakT = 0;
                    Mire(Tune.Elements.MudSeconds);
                    G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 24), "MUD", new Color(0.55f, 0.42f, 0.28f), 10, 0.8f);
                    G.Fx.Dust(GlobalPosition + new Vector2(0, HitRadius * 0.6f), 8, 2.2f, new Color(0.42f, 0.32f, 0.2f, 0.9f));
                }
                if (Interruptible) Interrupt(Vector2.Zero, Tune.Elements.StunSeconds * (mud ? Tune.Elements.MudStun : 1f), "STUNNED");
                else { Weaken(0.8f, Tune.Elements.StunSeconds * 2f); G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "STAGGERED", Elements.ColorOf(HeroElement.Earth), 9, 0.6f); }
                G.Fx.Ring(GlobalPosition, HitRadius + 8, new Color(Elements.ColorOf(HeroElement.Earth), 0.9f), 0.3f, FxLayer.RingStyle.Burst);
                break;
            case HeroElement.Nature:
                _natureBuild += amount;
                if (_natureBuild < full) break;
                _natureBuild = 0;
                // entangled: vines about it, slowing all it does, and it bleeds sap
                Entangled++;
                _tangleT = Tune.Elements.EntangleSeconds;
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

    /// <summary>An element seeps into it: <paramref name="perSecond"/> of it a second for <paramref name="seconds"/> (wind: a buffeting away
    /// from <paramref name="by"/>), replacing any seep already there.</summary>
    public void Seep(HeroElement el, float perSecond, float seconds, float power = 1f, Player by = null)
    {
        if (Dead || el == HeroElement.None) return;
        // (a copy keeps its own count, for the drain that tears it out; the host lays it on)
        if (Puppet) NetSync.EffectPuppet(this, NetSync.Effect.Seep, (float)el, perSecond, seconds);
        _seepEl = el; _seepRate = perSecond; _seepT = seconds; _seepTick = 0f; _seepPower = power; _seepBy = by;
    }

    /// <summary>What is still to seep in, torn out at once and cleared (the build-up that was coming).</summary>
    public float TakeSeep()
    {
        if (_seepT <= 0) return 0f;
        float left = _seepRate * _seepT;
        _seepT = 0f;
        if (Puppet) NetSync.EffectPuppet(this, NetSync.Effect.Seep, (float)_seepEl, 0f, 0f);
        return left;
    }

    private void TickSeep(float dt)
    {
        if (_seepT <= 0) return;
        _seepT -= dt;
        if ((_seepTick += dt) < 0.5f && _seepT > 0) return;
        float amount = _seepRate * _seepTick;
        _seepTick = 0f;
        if (Puppet) return;
        if (_seepEl == HeroElement.Wind)
        {
            var from = _seepBy != null && IsInstanceValid(_seepBy) ? _seepBy.GlobalPosition : GlobalPosition - Vector2.Right;
            var away = (GlobalPosition - from).LengthSquared() > 1f ? (GlobalPosition - from).Normalized() : Vector2.Right;
            Shove(away * amount * Tune.Elements.ShovePerBuild + new Vector2(0, -30f));
        }
        else Build(_seepEl, amount, _seepPower, _seepBy);
        if (G.Chance(0.7f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-HitRadius, HitRadius), G.Range(-HitRadius, HitRadius * 0.5f)), Elements.ColorOf(_seepEl));
    }

    /// <summary>A blow from this game's hero while the Aegis's element is lent to them (imbued): their element with it.</summary>
    private void Imbue(Player hero, DamageKind kind)
    {
        if (_imbuing || kind == DamageKind.Raw || Dead || !hero.Imbued(out var el)) return;
        _imbuing = true;
        try
        {
            if (el == HeroElement.Wind)
            {
                var away = (GlobalPosition - hero.GlobalPosition).LengthSquared() > 1f ? (GlobalPosition - hero.GlobalPosition).Normalized() : new Vector2(hero.Facing, 0);
                Shove(away * Tune.Elements.ImbueBuild * Tune.Elements.ShovePerBuild + new Vector2(0, -40f));
            }
            else Build(el, Tune.Elements.ImbueBuild, hero.Stats.DamageMult, hero);
        }
        finally { _imbuing = false; }
    }
    private static bool _imbuing;

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
        // (soaked, the ice was thicker: it bursts harder)
        float dmg = Tune.Elements.ShatterDamage * power * (Soaked ? Tune.Elements.SoakShatter : 1f);
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
        if (Ignited) FanFire();
    }

    /// <summary>Wind on the burning: the fire leaps to every creature near it (the same fire, as long as is left of it).</summary>
    private void FanFire()
    {
        var at = GlobalPosition;
        bool spread = false;
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == this || o.Dead || !o.CanBeHit || o.Ignited || o.GlobalPosition.DistanceTo(at) > Tune.Elements.WildfireRadius + o.HitRadius) continue;
            if (!G.Cave.LineClear(at, o.GlobalPosition)) continue;
            o.Ignite(_burnDps, Math.Max(1.5f, _burnT));
            o.Fanned++;
            spread = true;
            G.Fx.Beam(at, o.GlobalPosition, new Color(1f, 0.55f, 0.15f, 0.8f));
        }
        if (!spread) return;
        Fanned++;
        G.Fx.Text(at + new Vector2(0, -HitRadius - 24), "WILDFIRE", new Color(1f, 0.55f, 0.2f), 10, 0.8f);
        G.Fx.Burst(at, new Color(1f, 0.6f, 0.2f), 12, 180, 2.2f, 0.4f, -40);
    }

    /// <summary>
    /// How much of an element it takes by its nature (0: it refuses it): none of its own element (a fire creature can't be set alight, a
    /// frost one chilled, an earth one stunned by earth, a nature one entangled), half of earth for the armoured (they shrug off stuns), and
    /// no nature for what flies or swims (no roots reach it). Water, wind and dark take everything.
    /// </summary>
    public float StatusTake(HeroElement el) => (Element, el) switch
    {
        (Element.Fire, HeroElement.Fire) or (Element.Frost, HeroElement.Frost) or (Element.Earth, HeroElement.Earth) or (Element.Nature, HeroElement.Nature) => 0f,
        (Element.Armored, HeroElement.Earth) => Tune.Elements.ArmoredEarth,
        _ => el == HeroElement.Nature && !UsesGravity ? 0f : 1f,
    };

    /// <summary>For the tests: build-ups it has refused.</summary>
    public int Refused { get; private set; }
    private double _refuseShown;

    private void Refuse(HeroElement el)
    {
        Refused++;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _refuseShown < 1.0) return;
        _refuseShown = now;
        G.Fx.Text(GlobalPosition + new Vector2(0, -HitRadius - 12), "IMMUNE", new Color(Elements.ColorOf(el).Lerp(new Color(0.8f, 0.8f, 0.8f), 0.4f), 0.9f), 8, 0.6f);
    }

    /// <summary>How much faster (or slower) an element builds in it now: soaked and entangled are opposites (water: frost faster, fire
    /// slower; nature: fire faster, frost slower), and the blinded take every other element harder.</summary>
    public float ReactionMult(HeroElement el)
    {
        float m = 1f;
        if (_soakT > 0) m *= el == HeroElement.Frost ? Tune.Elements.SoakFrost : el == HeroElement.Fire ? Tune.Elements.SoakFire : 1f;
        if (_tangleT > 0) m *= el == HeroElement.Fire ? Tune.Elements.TangleFire : el == HeroElement.Frost ? Tune.Elements.TangleFrost : 1f;
        if (_blindT > 0 && el != HeroElement.Dark) m *= Tune.Elements.BlindAmp;
        return m;
    }

    /// <summary>Fire and frost meet in it: both go in a burst of scalding steam that hurts what's near, and leaves a cloud heroes hide in.</summary>
    private void Steam(float power, Player by)
    {
        Steamed++;
        Thaw();
        Quench();
        _chillT = 0;
        _fireBuild = _frostBuild = 0;
        var at = GlobalPosition;
        float r = Tune.Elements.SteamRadius, dmg = Tune.Elements.SteamDamage * power;
        var cloud = new SmokeCloud { Position = at, Radius = r, Life = Tune.Elements.SteamSeconds };
        G.Spawn(cloud);
        NetSync.HeroVisual(cloud);
        G.Fx.Burst(at, new Color(0.95f, 0.95f, 0.97f, 0.85f), 20, 150, 3.2f, 0.6f, -60);
        G.Fx.Ring(at, r, new Color(0.92f, 0.95f, 1f, 0.8f), 0.3f, FxLayer.RingStyle.Burst);
        G.Fx.Text(at + new Vector2(0, -HitRadius - 14), "STEAM", new Color(0.92f, 0.95f, 1f), 11, 0.8f);
        G.Sfx.Play("lava", at, -8, 0.05f, 1.6f);
        Credit(by, Hurt(dmg, Vector2.Zero, at, DamageKind.Raw));
        foreach (var o in G.Enemies.ToArray())
        {
            if (o == this || o.Dead || !o.CanBeHit || o.GlobalPosition.DistanceTo(at) > r + o.HitRadius || !G.Cave.LineClear(at, o.GlobalPosition)) continue;
            Credit(by, o.Hurt(dmg * 0.5f, (o.GlobalPosition - at).Normalized() * 80f, o.GlobalPosition, DamageKind.Raw));
        }
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
        if (_tangleT > 0) _tangleT -= dt;
        TickSeep(dt);
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
