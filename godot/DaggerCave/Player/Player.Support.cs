using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Every hero's support ability (the support button): one that does something for the others.
/// Swordsman: Battle Shout (everyone near hits harder). Warden: Taunt (every engaged creature turns on
/// her). Vitalist: Health Tap (health for vital force). Elementalist: Stalag-Might (the earth seizes a
/// creature). Rogue: Expose (a creature takes more from everyone). Aegis: Share Pain (a part of every
/// blow a friend takes falls on her). Shape Shifter: Pack Howl (everyone near runs and strikes faster). Automaton: Over-Pressure
/// (a long build-up, then it strikes, runs and jumps harder for a while).
/// </summary>
public partial class Player
{
    /// <summary>Test aid: act as another hero for the support button.</summary>
    public HeroKind? TestHero;
    private HeroKind SupportHero => TestHero ?? Stats.Hero;
    private float _supportCd, _supportBuf;
    private Vector2 _supportAim;
    private float _tauntLeft;

    /// <summary>Seconds the support ability takes to come back.</summary>
    public float SupportRecharge => SupportHero switch
    {
        HeroKind.Swordsman => Tune.Support.ShoutCooldown,
        HeroKind.Warden => Tune.Support.TauntCooldown,
        HeroKind.Vitalist => Tune.Support.TapCooldown,
        HeroKind.Elementalist => Tune.Support.StalagCooldown,
        HeroKind.Rogue => Tune.Support.ExposeCooldown,
        HeroKind.Aegis => Tune.Support.PainCooldown,
        HeroKind.Automaton => Tune.Support.PressurePress,
        _ => Tune.Support.HowlCooldown,
    } * Stats.AbilityCdMult;

    public string SupportName => SupportHero switch
    {
        HeroKind.Swordsman => "SHOUT",
        HeroKind.Warden => "TAUNT",
        HeroKind.Vitalist => "TAP",
        HeroKind.Elementalist => "STALAG",
        HeroKind.Rogue => "EXPOSE",
        HeroKind.Aegis => "SHARE",
        HeroKind.Automaton => "PRESSURIZE",
        _ => "HOWL",
    };

    public float SupportCooldownFrac => Math.Clamp(_supportCd / Math.Max(0.01f, SupportRecharge), 0f, 1f);
    public bool SupportReady => _supportCd <= 0;
    public bool Taunting => IsRemote ? (_netFlags & HfTaunt) != 0 : _tauntLeft > 0;
    public float TauntLeft => _tauntLeft;
    /// <summary>How far off the creatures count this hero to be: the hero's own reckoning, or next to nothing while taunting.</summary>
    public float EffectiveThreat => Taunting ? 0.001f : (Stats?.ThreatDist ?? 1f) * (Form?.Threat ?? 1f);

    private void TickSupport(float dt)
    {
        if (_supportCd > 0) _supportCd -= dt;
        _supportBuf -= dt;
        if (_tauntLeft > 0)
        {
            _tauntLeft -= dt;
            if (G.Chance(dt * 6f)) G.Fx.Ring(GlobalPosition + new Vector2(0, -8), 18, new Color(1f, 0.55f, 0.3f, 0.6f), 0.4f, FxLayer.RingStyle.Ping);
        }
        TickBuffs(dt);
    }

    /// <summary>Test aids: press the support button once with it ready; let buffs and a taunt run out.</summary>
    public bool TestSupport(Vector2 aim) { _supportCd = 0; return Support(aim); }
    public void TestExpire() { TickBuffs(999f); _tauntLeft = 0; }

    /// <summary>The support button.</summary>
    private bool Support(Vector2 aim)
    {
        if (_snagT > 0 || _supportCd > 0) return false;
        bool used = SupportHero switch
        {
            HeroKind.Swordsman => TryShout(),
            HeroKind.Warden => TryTaunt(),
            HeroKind.Vitalist => TryHealthTap(),
            HeroKind.Elementalist => TryStalag(aim),
            HeroKind.Rogue => TryExpose(aim),
            HeroKind.Aegis => TrySharePain(aim),
            HeroKind.Automaton => TryOverPressure(),
            _ => TryHowl(),
        };
        // (an ability that starts its own wait, as Pressurize does when its pressure goes, keeps the longer one)
        if (used) _supportCd = Math.Max(_supportCd, SupportRecharge);
        return used;
    }

    /// <summary>Every living hero within range of this one (this one too).</summary>
    private List<Player> HeroesNear(float range)
    {
        var list = new List<Player>();
        foreach (var h in G.Players)
            if (h != null && IsInstanceValid(h) && !h.Dead && (h == this || h.GlobalPosition.DistanceTo(GlobalPosition) <= range)) list.Add(h);
        return list;
    }

    // ---------------------------------------------------------------- timed buffs

    public const int BuffShout = 1, BuffHowl = 2;
    private readonly Dictionary<int, float> _buffs = new();
    public bool HasBuff(int kind) => _buffs.ContainsKey(kind);
    public float BuffLeft(int kind) => _buffs.TryGetValue(kind, out var t) ? t : 0f;

    /// <summary>A friend's call (or your own): for <paramref name="seconds"/>, hit harder (Shout) or move and strike faster (Howl).</summary>
    public void GiveBuff(int kind, float seconds)
    {
        if (Dead) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Buff, seconds, 0, 0, kind); return; }
        if (!_buffs.ContainsKey(kind)) ApplyBuff(kind, +1);
        _buffs[kind] = Math.Max(BuffLeft(kind), seconds);
        G.Fx.Ring(GlobalPosition + new Vector2(0, -6), 20, kind == BuffShout ? new Color(1f, 0.6f, 0.3f, 0.8f) : new Color(0.85f, 0.95f, 1f, 0.8f), 0.4f, FxLayer.RingStyle.Dashed);
    }

    private void ApplyBuff(int kind, int dir)
    {
        float m(float x) => dir > 0 ? x : 1f / x;
        if (kind == BuffShout) Stats.DamageMult *= m(Tune.Support.ShoutDamage);
        else if (kind == BuffHowl) { Stats.MoveSpeed *= m(Tune.Support.HowlMove); Stats.AttackSpeed *= m(Tune.Support.HowlAttack); }
    }

    private void TickBuffs(float dt)
    {
        if (_buffs.Count == 0) return;
        foreach (var kind in _buffs.Keys.ToList())
        {
            if ((_buffs[kind] -= dt) > 0) continue;
            _buffs.Remove(kind);
            ApplyBuff(kind, -1);
        }
    }

    // ---------------------------------------------------------------- the seven

    private bool TryShout()
    {
        foreach (var h in HeroesNear(Tune.Support.ShoutRange)) h.GiveBuff(BuffShout, Tune.Support.ShoutSeconds);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, Tune.Support.ShoutRange * 0.6f, new Color(1f, 0.6f, 0.3f, 0.7f), 0.5f, FxLayer.RingStyle.Dashed);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 40, new Color(1f, 0.8f, 0.5f, 0.5f), 0.4f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "BATTLE SHOUT", new Color(1f, 0.75f, 0.4f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -2, 0.05f, 1.2f);
        Anim?.Once("cast", 3, 1.4f);
        return true;
    }

    private bool TryHowl()
    {
        foreach (var h in HeroesNear(Tune.Support.HowlRange)) h.GiveBuff(BuffHowl, Tune.Support.HowlSeconds);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, Tune.Support.HowlRange * 0.6f, new Color(0.85f, 0.95f, 1f, 0.7f), 0.5f, FxLayer.RingStyle.Dashed);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "PACK HOWL", new Color(0.9f, 0.95f, 1f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -2, 0.05f, 1.5f);
        return true;
    }

    private bool TryTaunt()
    {
        _tauntLeft = Tune.Support.TauntSeconds;
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Ring(at, 60, new Color(1f, 0.5f, 0.3f, 0.8f), 0.5f, FxLayer.RingStyle.Ping);
        G.Fx.Shockwave(GlobalPosition + new Vector2(0, 12), 50, new Color(1f, 0.6f, 0.4f, 0.5f), 0.4f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -34), "TAUNT", new Color(1f, 0.6f, 0.35f), 12, 1f);
        G.Sfx.Play("roar", GlobalPosition, -1, 0.05f, 0.7f);
        // every creature near looks for its mark again, at once
        foreach (var e in G.Enemies) if (IsInstanceValid(e)) e.ForgetTarget();
        return true;
    }

    private bool TryHealthTap()
    {
        float cost = Stats.MaxHp * Tune.Support.TapCost;
        if (Hp <= cost + 1f) { SayNo("TOO WEAK"); _supportCd = 0.5f; return false; }
        Hp -= cost;
        GainVitalForce(Stats.VitalForceMax * Tune.Support.TapGain);
        var at = GlobalPosition + new Vector2(0, -8);
        G.Fx.Burst(at, new Color(0.8f, 0.1f, 0.15f), 14, 140, 2.2f, 0.4f, 60);
        G.Fx.Converge(at, 34, LifeColorLight, 12, 0.35f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -22), "-" + Math.Max(1, (int)Math.Ceiling(cost)), new Color(0.85f, 0.15f, 0.2f), 13);
        G.Fx.Text(GlobalPosition + new Vector2(0, -36), "HEALTH TAP", LifeColorLight, 11, 0.9f);
        Anim?.Flash(0.6f);
        G.Sfx.Play("drain", GlobalPosition, -4, 0.05f, 0.8f);
        return true;
    }

    private bool TryStalag(Vector2 aim)
    {
        var dir = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        // the earth can only seize what stands (or crouches, or lies) near the ground
        var foe = FindSpellTarget(dir, Tune.Support.StalagRange, 40f, e => StalagFloor(e, out _));
        if (foe == null)
        {
            SayNo(FindSpellTarget(dir, Tune.Support.StalagRange, 40f) != null ? "NOT NEAR THE GROUND" : "NOTHING TO SEIZE");
            _supportCd = 0.5f; return false;
        }
        // (the earth is called first: the Elementalist holds still, animated, for a whole second, and then it answers)
        LastCast = "stalag";
        Hold(Tune.Support.StalagDelay, "hex", () =>
        {
            if (Dead || !IsInstanceValid(foe) || foe.Dead || !StalagFloor(foe, out var floor)) return;
            var at = foe.GlobalPosition + new Vector2(0, foe.HitRadius * 0.6f);
            foe.Hurt(Tune.Support.StalagDamage * Stats.DamageMult, Vector2.Zero, at, DamageKind.Physical);
            if (!foe.Dead) foe.Interrupt(Vector2.Zero, Tune.Support.StalagSeconds, "ROOTED");
            // a bulge of rock bursts up round its feet, holds it, and sinks away when the hold is over
            if (!foe.Dead) G.World.AddChild(new StalagGrip { Position = floor, Foe = foe, Seconds = Tune.Support.StalagSeconds, Radius = Math.Max(10f, foe.HitRadius) });
            // spikes of rock thrust up under it
            for (int k = -2; k <= 2; k++)
            {
                var p = at + new Vector2(k * 7, 0);
                G.Fx.Directional(p, Vector2.Up, 0.2f, new Color(0.55f, 0.47f, 0.38f), 4, 200, 2.4f, 0.4f, 300, 1);
            }
            G.Fx.Debris(at, new Color(0.5f, 0.43f, 0.35f), 16, 220);
            G.Fx.Shockwave(at + new Vector2(0, 6), 26, new Color(0.8f, 0.7f, 0.5f, 0.5f), 0.3f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -34), "STALAG-MIGHT!", new Color(0.85f, 0.75f, 0.55f), 11, 0.9f);
            G.Sfx.Play("rock", at, -1, 0.1f, 0.8f);
            _castGlow = 1f;
        });
        return true;
    }

    /// <summary>Where the ground is under a creature, if it is near enough to it (within a body's height of its feet).</summary>
    private static bool StalagFloor(Enemy e, out Vector2 floor)
    {
        floor = default;
        if (!G.Cave.FindFloor(e.GlobalPosition, e.HitRadius + Tune.Support.StalagGroundReach, out floor)) return false;
        return !G.Cave.IsWater(e.GlobalPosition);
    }

    private bool TryExpose(Vector2 aim)
    {
        var foe = FindSpellTarget(aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0), Tune.Support.ExposeRange, 40f);
        if (foe == null) { SayNo("NOTHING TO MARK"); _supportCd = 0.5f; return false; }
        foe.Expose(Tune.Support.ExposeVuln, Tune.Support.ExposeSeconds);
        G.Fx.Ring(foe.GlobalPosition, foe.HitRadius + 10, new Color(1f, 0.85f, 0.3f, 0.9f), 0.4f, FxLayer.RingStyle.Ping);
        G.Fx.Text(foe.HeadPoint(10f), "EXPOSED", new Color(1f, 0.85f, 0.35f), 11, 0.9f);
        G.Sfx.Play("throw", GlobalPosition, -6, 0.1f, 1.3f);
        return true;
    }

    // ---- Share Pain (the Aegis): a friend's pain, a part of it, on her
    /// <summary>Whom this Aegis shares the pain of (null: no one).</summary>
    public Player PainTarget { get; private set; }
    private int _painBy = -1;
    private float _painShare;
    /// <summary>An Aegis shares this hero's pain.</summary>
    public bool PainShared => _painShare > 0f;

    /// <summary>The friend aimed at: a part of each blow they take falls on her now (aimed at again, or another picked, it lifts).</summary>
    private bool TrySharePain(Vector2 aim)
    {
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var ally = PickAlly(aim);
        if (ally == null) { SayNo("NO ONE TO SHARE WITH"); _supportCd = 0.5f; return false; }
        // (one at a time: the old one's pain is theirs again)
        var old = PainTarget;
        if (old != null && IsInstanceValid(old)) old.GivePain(Net.Me, 0f);
        PainTarget = null;
        if (old == ally)
        {
            G.Fx.Text(ally.GlobalPosition + new Vector2(0, -28), "UNSHARED", new Color(0.85f, 0.85f, 0.8f), 9, 0.7f);
            return true;
        }
        PainTarget = ally;
        ally.GivePain(Net.Me, Tune.Support.PainShare);
        G.Fx.Beam(CastPoint, ally.GlobalPosition + new Vector2(0, -6), new Color(1f, 0.6f, 0.55f, 0.9f));
        G.Sfx.Play("heal", CastPoint, -8, 0.05f, 0.7f);
        return true;
    }

    /// <summary>An Aegis shares (share &gt; 0) or stops sharing (0) this hero's pain.</summary>
    public void GivePain(int aegisId, float share)
    {
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Pain, share, 0, 0, aegisId); return; }
        _painBy = aegisId; _painShare = share;
        if (share > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -28), "PAIN SHARED", new Color(1f, 0.65f, 0.6f), 9, 0.8f);
    }

    /// <summary>The Aegis's share of a blow on this hero goes to her; what's left is this hero's.</summary>
    private float SharePain(float dmg)
    {
        if (_painShare <= 0f || dmg <= 0f) return dmg;
        float shared = dmg * _painShare;
        if (_painBy == Net.Me) { if (G.Player == null || G.Player.Dead || G.Player == this) return dmg; G.Player.TakePain(shared); }
        else NetSync.SendBurden(_painBy, shared);
        return dmg - shared;
    }

    /// <summary>The Aegis takes the share of a friend's blow (through her own armour; no flinch).</summary>
    public void TakePain(float amount)
    {
        if (Dead || IsRemote) return;
        amount = Soften(amount * (1f - Stats.DamageReduction) * Stats.DamageTakenMult);
        if (amount <= 0.01f) return;
        RunMetrics.Took(amount, null, "Friends' blows (shared)");
        TakeRawDamage(amount, "chip");
        Anim.Flash(0.4f);
    }
}
